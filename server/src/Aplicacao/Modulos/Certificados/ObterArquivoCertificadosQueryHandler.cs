using FluentResults;
using GeradorCertificadosOnline.Aplicacao.Modulos.Certificados.DTOs;
using GeradorCertificadosOnline.Aplicacao.Modulos.Certificados.Util;
using GeradorCertificadosOnline.Dominio.Modulos.Certificados;
using GeradorCertificadosOnline.Dominio.Modulos.Cursos;
using GeradorCertificadosOnline.Aplicacao.Modulos.Cursos.Util;
using MediatR;

namespace GeradorCertificadosOnline.Aplicacao.Modulos.Certificados;

public sealed record ObterArquivoCertificadosQuery(Guid CursoId, Guid UsuarioId)
    : IRequest<Result<ArquivoCertificadosDto>>;

public sealed class ObterArquivoCertificadosQueryHandler(
    IRepositorioCurso repositorioCurso,
    IRepositorioProcessamentoCertificados repositorioProcessamento,
    ICertificadoStorage certificadoStorage)
    : IRequestHandler<ObterArquivoCertificadosQuery, Result<ArquivoCertificadosDto>>
{
    public async Task<Result<ArquivoCertificadosDto>> Handle(
        ObterArquivoCertificadosQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!await repositorioCurso.ExisteAsync(query.CursoId, query.UsuarioId, cancellationToken))
            return Result.Fail(ErrosDeCurso.NaoEncontrado());

        var processamento = await repositorioProcessamento.SelecionarPorCursoAsync(
            query.CursoId,
            cancellationToken);

        if (processamento is null)
            return Result.Fail(ErrosDeCertificado.ProcessamentoNaoEncontrado());

        if (!processamento.EstaFinalizado || processamento.CaminhoZip is null)
            return Result.Fail(ErrosDeCertificado.ZipIndisponivel());

        cancellationToken.ThrowIfCancellationRequested();

        return Result.Ok(new ArquivoCertificadosDto(
            certificadoStorage.AbrirLeitura(processamento.CaminhoZip),
            $"certificados-{query.CursoId}.zip",
            "application/zip")
        );
    }
}
