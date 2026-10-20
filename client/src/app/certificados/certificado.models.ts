export interface SolicitarCertificadosRequest {
  readonly alunos: readonly { readonly nome: string }[];
}

export interface SolicitacaoCertificadosDto {
  readonly cursoId: string;
  readonly totalAlunos: number;
  readonly status: string;
}

export interface StatusProcessamentoDto {
  readonly cursoId: string;
  readonly status: 'Pendente' | 'GerandoCertificados' | 'Concluido' | 'ConcluidoComFalhas';
  readonly totalCertificados: number;
  readonly gerados: number;
  readonly falhas: number;
  readonly zipDisponivel: boolean;
  readonly iniciadoEm: string;
  readonly concluidoEm: string | null;
}

export interface CertificadoDto {
  readonly id: string;
  readonly nomeAluno: string;
  readonly statusGeracao: 'Pendente' | 'Gerado' | 'Falha';
  readonly geradoEm: string | null;
}
