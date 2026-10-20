using FluentResults;
using GeradorCertificadosOnline.Aplicacao.Modulos.Certificados.DTOs;
using GeradorCertificadosOnline.Aplicacao.Modulos.Certificados.Util;
using GeradorCertificadosOnline.Dominio.Modulos.Certificados;
using GeradorCertificadosOnline.Dominio.Modulos.Cursos;
using GeradorCertificadosOnline.Aplicacao.Modulos.Cursos.Util;
using MediatR;

namespace GeradorCertificadosOnline.Aplicacao.Modulos.Certificados;

public sealed record ObterStatusProcessamentoQuery(Guid CursoId, Guid UsuarioId)
    : IRequest<Result<StatusProcessamentoDto>>;

public sealed class ObterStatusProcessamentoQueryHandler(
    IRepositorioCurso repositorioCurso,
    IRepositorioProcessamentoCertificados repositorioProcessamento)
    : IRequestHandler<ObterStatusProcessamentoQuery, Result<StatusProcessamentoDto>>
{
    public async Task<Result<StatusProcessamentoDto>> Handle(
        ObterStatusProcessamentoQuery query,
        CancellationToken cancellationToken
    )
    {
        if (!await repositorioCurso.ExisteAsync(query.CursoId, query.UsuarioId, cancellationToken))
            return Result.Fail(ErrosDeCurso.NaoEncontrado());

        var processamento = await repositorioProcessamento.SelecionarPorCursoAsync(
            query.CursoId,
            cancellationToken);

        return processamento is null
            ? Result.Fail(ErrosDeCertificado.ProcessamentoNaoEncontrado())
            : Result.Ok(StatusProcessamentoDto.From(processamento));
    }
}
