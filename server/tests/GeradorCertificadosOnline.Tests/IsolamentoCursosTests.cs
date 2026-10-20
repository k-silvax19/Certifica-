using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GeradorCertificadosOnline.Api.Compartilhado.Auth;
using GeradorCertificadosOnline.Api.Modulos.Cursos;
using GeradorCertificadosOnline.Aplicacao.Modulos.Cursos;
using GeradorCertificadosOnline.Aplicacao.Modulos.Cursos.DTOs;
using GeradorCertificadosOnline.Dominio.Compartilhado.Auth;
using GeradorCertificadosOnline.Dominio.Modulos.Certificados;
using GeradorCertificadosOnline.Dominio.Modulos.Cursos;
using GeradorCertificadosOnline.Infraestrutura.Compartilhado.Orm;
using GeradorCertificadosOnline.Infraestrutura.Modulos.Certificados;
using GeradorCertificadosOnline.Infraestrutura.Modulos.Cursos;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GeradorCertificadosOnline.Tests;

[TestClass]
public sealed class IsolamentoCursosTests
{
    private readonly Guid usuarioA = Guid.NewGuid();
    private readonly Guid usuarioB = Guid.NewGuid();
    private SqliteConnection conexao = null!;
    private WebApplication app = null!;
    private HttpClient cliente = null!;
    private Curso cursoA = null!;
    private Curso cursoB = null!;
    private Guid cursoLegadoId;
    private readonly Mock<ISendEndpointProvider> mensageria = new(MockBehavior.Strict);
    private readonly Mock<ICertificadoStorage> storage = new(MockBehavior.Strict);

    [TestInitialize]
    public async Task Preparar()
    {
        conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "testes-isolamento",
            ["Jwt:Audience"] = "testes-isolamento",
            ["Jwt:SigningKey"] = "chave-exclusiva-de-testes-com-mais-de-32-bytes",
            ["Jwt:ExpiresInMinutes"] = "10"
        });
        builder.Services.AddJwtAuthServices();
        builder.Services.AddControllers().AddApplicationPart(typeof(CursosController).Assembly);
        builder.Services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ListarCursosQuery).Assembly));
        builder.Services.AddDbContext<CertificadosDbContext>(options => options.UseSqlite(conexao));
        builder.Services.AddScoped<IRepositorioCurso, RepositorioCursoEmOrm>();
        builder.Services.AddScoped<IRepositorioProcessamentoCertificados, RepositorioProcessamentoCertificadosEmOrm>();
        builder.Services.AddSingleton(mensageria.Object);
        builder.Services.AddSingleton(storage.Object);
        app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CertificadosDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(
                new IdentityUser<Guid> { Id = usuarioA, UserName = "a@teste.local" },
                new IdentityUser<Guid> { Id = usuarioB, UserName = "b@teste.local" });
            cursoA = new Curso(usuarioA, "Curso A", 40, new DateOnly(2026, 10, 20));
            cursoB = new Curso(usuarioB, "Curso B", 80, new DateOnly(2026, 10, 20));
            var legado = new Curso(usuarioA, "Curso sem autoria", 20, new DateOnly(2026, 10, 20));
            db.Cursos.AddRange(cursoA, cursoB, legado);
            db.Entry(legado).Property(c => c.UsuarioId).CurrentValue = null;
            cursoLegadoId = legado.Id;
            db.ProcessamentosCertificados.Add(new ProcessamentoCertificados(cursoB.Id, [new Certificado("Aluno B")]));
            await db.SaveChangesAsync();
        }

        await app.StartAsync();
        cliente = app.GetTestClient();
        Autenticar(usuarioA);
    }

    private void Autenticar(Guid usuarioId)
    {
        var token = app.Services.GetRequiredService<IEmissorTokens>().CriarToken(usuarioId, "teste@teste.local");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    [TestCleanup]
    public async Task Encerrar()
    {
        cliente?.Dispose();
        if (app is not null) await app.DisposeAsync();
        if (conexao is not null) await conexao.DisposeAsync();
    }

    [TestMethod]
    public async Task CadaContaListaSomenteSeusCursos()
    {
        foreach (var (usuario, curso) in new[] { (usuarioA, cursoA), (usuarioB, cursoB) })
        {
            Autenticar(usuario);
            var cursos = await cliente.GetFromJsonAsync<CursoDto[]>("/cursos");
            Assert.IsNotNull(cursos);
            Assert.AreEqual(1, cursos.Length);
            Assert.AreEqual(curso.Id, cursos[0].Id);
            Assert.AreEqual(HttpStatusCode.OK, (await cliente.GetAsync($"/cursos/{curso.Id}")).StatusCode);
        }
    }

    [TestMethod]
    public async Task CadastroUsaUsuarioDoTokenMesmoComUsuarioIdNoJson()
    {
        var resposta = await cliente.PostAsJsonAsync("/cursos", new
        {
            usuarioId = usuarioB,
            nome = "Curso novo", cargaHoraria = 30, dataConclusao = "2026-10-20"
        });
        Assert.AreEqual(HttpStatusCode.Created, resposta.StatusCode);
        var curso = await resposta.Content.ReadFromJsonAsync<CursoDto>();
        Assert.IsNotNull(curso);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CertificadosDbContext>();
        Assert.AreEqual(usuarioA, (await db.Cursos.SingleAsync(c => c.Id == curso.Id)).UsuarioId);
        Autenticar(usuarioB);
        Assert.AreEqual(HttpStatusCode.NotFound, (await cliente.GetAsync($"/cursos/{curso.Id}")).StatusCode);
    }

    [TestMethod]
    public async Task IdDeOutroUsuarioNaoPermiteConsultarCursoCertificadosStatusOuDownload()
    {
        foreach (var sufixo in new[] { "", "/certificados", "/status", "/certificados/download" })
        {
            var resposta = await cliente.GetAsync($"/cursos/{cursoB.Id}{sufixo}");
            Assert.AreEqual(HttpStatusCode.NotFound, resposta.StatusCode, sufixo);
            Assert.IsFalse((await resposta.Content.ReadAsStringAsync()).Contains("Aluno B"));
        }
        storage.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task NaoPermiteGerarCertificadosDeOutroUsuario()
    {
        var resposta = await cliente.PostAsJsonAsync($"/cursos/{cursoB.Id}/certificados", new
        {
            alunos = new[] { new { nome = "Aluno indevido" } }
        });
        Assert.AreEqual(HttpStatusCode.NotFound, resposta.StatusCode);
        mensageria.VerifyNoOtherCalls();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CertificadosDbContext>();
        Assert.AreEqual(1, await db.ProcessamentosCertificados.CountAsync());
    }

    [TestMethod]
    public async Task DonoPodeConsultarCertificadosEStatus()
    {
        Autenticar(usuarioB);
        Assert.AreEqual(HttpStatusCode.OK, (await cliente.GetAsync($"/cursos/{cursoB.Id}/status")).StatusCode);
        var resposta = await cliente.GetAsync($"/cursos/{cursoB.Id}/certificados");
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        StringAssert.Contains(await resposta.Content.ReadAsStringAsync(), "Aluno B");
        Assert.AreEqual(HttpStatusCode.Conflict, (await cliente.GetAsync($"/cursos/{cursoB.Id}/certificados/download")).StatusCode);
    }

    [TestMethod]
    public async Task CursoLegadoSemDonoNaoFicaAcessivelPorId()
    {
        foreach (var usuario in new[] { usuarioA, usuarioB })
        {
            Autenticar(usuario);
            Assert.AreEqual(HttpStatusCode.NotFound, (await cliente.GetAsync($"/cursos/{cursoLegadoId}")).StatusCode);
        }
    }

    [TestMethod]
    public async Task SemTokenNaoPodeAcessarCursosOuCertificados()
    {
        cliente.DefaultRequestHeaders.Authorization = null;
        foreach (var rota in new[] { "/cursos", $"/cursos/{cursoB.Id}", $"/cursos/{cursoB.Id}/status", $"/cursos/{cursoB.Id}/certificados", $"/cursos/{cursoB.Id}/certificados/download" })
            Assert.AreEqual(HttpStatusCode.Unauthorized, (await cliente.GetAsync(rota)).StatusCode, rota);
    }
}
