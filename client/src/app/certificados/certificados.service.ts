import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';

import { URL_API } from '../auth/auth.tokens';
import {
  CertificadoDto,
  SolicitacaoCertificadosDto,
  SolicitarCertificadosRequest,
  StatusProcessamentoDto,
} from './certificado.models';

@Service()
export class CertificadosService {
  private readonly http = inject(HttpClient);
  private readonly urlApi = inject(URL_API);

  solicitarGeracao(
    cursoId: string,
    requisicao: SolicitarCertificadosRequest,
  ): Observable<SolicitacaoCertificadosDto> {
    return this.http.post<SolicitacaoCertificadosDto>(
      `${this.urlApi}/cursos/${cursoId}/certificados`,
      requisicao,
    );
  }

  obterStatus(cursoId: string): Observable<StatusProcessamentoDto> {
    return this.http.get<StatusProcessamentoDto>(`${this.urlApi}/cursos/${cursoId}/status`);
  }

  listar(cursoId: string): Observable<readonly CertificadoDto[]> {
    return this.http.get<readonly CertificadoDto[]>(
      `${this.urlApi}/cursos/${cursoId}/certificados`,
    );
  }

  baixar(cursoId: string): Observable<Blob> {
    return this.http.get(`${this.urlApi}/cursos/${cursoId}/certificados/download`, {
      responseType: 'blob',
    });
  }
}
