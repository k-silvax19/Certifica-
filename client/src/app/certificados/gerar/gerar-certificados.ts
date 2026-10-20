import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { form, FormField, required } from '@angular/forms/signals';
import { exhaustMap, forkJoin, Subscription, takeWhile, timeout, timer } from 'rxjs';

import { ApiValidationProblemDetails } from '../../auth/auth-models';
import { CertificadoDto, StatusProcessamentoDto } from '../certificado.models';
import { CertificadosService } from '../certificados.service';

@Component({
  imports: [FormField],
  selector: 'app-gerar-certificados',
  templateUrl: './gerar-certificados.html',
})
export class GerarCertificados implements OnInit {
  readonly cursoId = input.required<string>();

  private readonly certificadosService = inject(CertificadosService);
  private readonly destroyRef = inject(DestroyRef);
  private acompanhamento?: Subscription;

  protected readonly modelo = signal({ nomes: '' });
  protected readonly formulario = form(this.modelo, (modelo) => {
    required(modelo.nomes, { message: 'Informe ao menos um aluno.' });
  });
  protected readonly status = signal<StatusProcessamentoDto | null>(null);
  protected readonly certificados = signal<readonly CertificadoDto[]>([]);
  protected readonly carregando = signal(true);
  protected readonly enviando = signal(false);
  protected readonly baixando = signal(false);
  protected readonly mensagemErro = signal('');
  protected readonly mensagemSucesso = signal('');
  protected readonly emAndamento = computed(() => {
    const status = this.status();
    return !!status && !status.concluidoEm;
  });

  ngOnInit(): void {
    this.acompanharGeracao(true);
  }

  protected gerar(event: SubmitEvent): void {
    event.preventDefault();
    if (this.enviando() || this.carregando() || this.emAndamento()) return;

    this.mensagemErro.set('');
    this.mensagemSucesso.set('');
    if (this.formulario().invalid()) {
      this.formulario().markAsTouched();
      return;
    }

    const alunos = this.modelo()
      .nomes.split(/\r?\n/)
      .map((nome) => ({ nome: nome.trim() }))
      .filter((aluno) => aluno.nome.length > 0);

    if (alunos.length === 0 || alunos.some((aluno) => aluno.nome.length > 200)) {
      this.mensagemErro.set('Informe ao menos um aluno, com no máximo 200 caracteres por nome.');
      return;
    }

    this.enviando.set(true);
    this.certificadosService
      .solicitarGeracao(this.cursoId(), { alunos })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.enviando.set(false);
          this.modelo.set({ nomes: '' });
          this.mensagemSucesso.set('Geração solicitada. Acompanhe o progresso abaixo.');
          this.status.set(null);
          this.certificados.set([]);
          this.acompanharGeracao();
        },
        error: (resposta: HttpErrorResponse) => {
          this.enviando.set(false);
          this.mensagemErro.set(this.obterMensagemErro(resposta));
          if (resposta.status === 409) this.acompanharGeracao();
        },
      });
  }

  protected acompanharGeracao(permitirSemLote = false): void {
    this.acompanhamento?.unsubscribe();
    this.carregando.set(true);
    this.mensagemErro.set('');
    this.acompanhamento = timer(0, 2000)
      .pipe(
        exhaustMap(() =>
          forkJoin({
            status: this.certificadosService.obterStatus(this.cursoId()),
            certificados: this.certificadosService.listar(this.cursoId()),
          }).pipe(timeout(15000)),
        ),
        takeWhile((resultado) => !resultado.status.concluidoEm, true),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (resultado) => {
          this.status.set(resultado.status);
          this.certificados.set(resultado.certificados);
          this.carregando.set(false);
        },
        error: (resposta: HttpErrorResponse) => {
          this.carregando.set(false);
          if (permitirSemLote && resposta.status === 404) return;
          this.mensagemErro.set(this.obterMensagemErro(resposta));
        },
      });
  }

  protected baixar(): void {
    if (this.baixando() || !this.status()?.zipDisponivel) return;
    this.baixando.set(true);
    this.mensagemErro.set('');
    this.certificadosService
      .baixar(this.cursoId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (arquivo) => {
          const url = URL.createObjectURL(arquivo);
          const link = document.createElement('a');
          link.href = url;
          link.download = `certificados-${this.cursoId()}.zip`;
          link.click();
          setTimeout(() => URL.revokeObjectURL(url), 1000);
          this.baixando.set(false);
        },
        error: async (resposta: HttpErrorResponse) => {
          this.baixando.set(false);
          if (resposta.error instanceof Blob) {
            try {
              const problem = JSON.parse(
                await resposta.error.text(),
              ) as ApiValidationProblemDetails;
              this.mensagemErro.set(problem.detail ?? 'Não foi possível baixar os certificados.');
              return;
            } catch {
              /* A resposta pode não conter um ProblemDetails. */
            }
          }
          this.mensagemErro.set(this.obterMensagemErro(resposta));
        },
      });
  }

  protected descreverStatus(status: string): string {
    const descricoes: Record<string, string> = {
      Pendente: 'Aguardando geração',
      GerandoCertificados: 'Gerando certificados',
      Concluido: 'Concluído',
      ConcluidoComFalhas: 'Concluído com falhas',
      Gerado: 'Gerado',
      Falha: 'Falha na geração',
    };
    return descricoes[status] ?? status;
  }

  private obterMensagemErro(resposta: HttpErrorResponse): string {
    const problem = resposta.error as ApiValidationProblemDetails | null;
    if (problem?.errors) {
      const mensagens = Object.values(problem.errors).flat();
      if (mensagens.length > 0) return mensagens.join('\n');
    }
    return (
      problem?.detail ?? 'Não foi possível consultar ou gerar os certificados. Tente novamente.'
    );
  }
}
