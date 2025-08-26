using System.IdentityModel.Tokens.Jwt;
using System.Runtime.Intrinsics.Arm;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using minimal_api.Dominio.DTOs;
using minimal_api.Dominio.EntIdades;
using minimal_api.Dominio.Enuns;
using minimal_api.Dominio.ModelViews;
using minimal_api.Dominio.Servicos;
using minimal_api.Infraestrutura.Db;
using minimal_api.Infraestrutura.Interfaces;


#region Builder
var builder = WebApplication.CreateBuilder(args);
var Key = builder.Configuration.GetSection("Jwt").ToString();


builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(option =>
{
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateIssuer = false,
        ValidateAudience = false
    };

});

builder.Services.AddAuthorization();



builder.Configuration.AddEnvironmentVariables();

builder.Services.AddDbContext<DbContexto>(options =>
{
    options.UseMySql(
        builder.Configuration.GetConnectionString("MySql"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MySql"))
    );
});

builder.Services.AddScoped<IAdministradorServico, AdministradorServico>();
builder.Services.AddScoped<IVeiculoServico, VeiculoServico>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT aqui"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();
#endregion


#region Home
app.MapGet("/", () => Results.Json(new Home())).AllowAnonymous().WithTags("Home");
#endregion

#region Administrador
string GerarTokenJwt(Administrador administrador)
{
    if (string.IsNullOrEmpty(Key)) return string.Empty;

    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>()
    {
        new Claim("Email", administrador.Email),
        new Claim("Perfil", administrador.Perfil),
        new Claim(ClaimTypes.Role, administrador.Perfil),
    };

    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.Now.AddDays(1),
        signingCredentials: credentials

    );

    return new JwtSecurityTokenHandler().WriteToken(token);

}

app.MapPost("/Administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) =>

{

    var adm = administradorServico.Login(loginDTO);
    if (adm != null)
    {
        string token = GerarTokenJwt(adm);
        return Results.Ok(new AdministradorLogado
        {
            Email = adm.Email,
            Perfil = adm.Perfil,
            Token = token
        });
    }
    else
    {
        return Results.Unauthorized();
    }
}).AllowAnonymous().RequireAuthorization().RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" }).WithTags("Administradores");


app.MapGet("/Administradores", ([FromQuery] int? pagina, IAdministradorServico administradorServico) =>
{
    var adms = new List<AdministradorModelView>();
    var administradores = administradorServico.Todos(pagina);

    foreach (var adm in administradores)
    {
        adms.Add(new AdministradorModelView
        {
            Id = adm.Id,
            Email = adm.Email,
            Perfil = adm.Perfil,

        });
    }

    return Results.Ok(adms);

}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
.WithTags("Administradores");


app.MapGet("/Administradores/{Id}", ([FromRoute] int Id, IAdministradorServico administradorServico) =>
{

    var administrador = administradorServico.BuscaPorId(Id);

    if (administrador == null) return Results.NotFound();

    return Results.Ok(new AdministradorModelView
    {
        Id = administrador.Id,
        Email = administrador.Email,
        Perfil = administrador.Perfil,

    });

}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
.WithTags("Administradores");


app.MapPost("/Administradores", ([FromBody] AdministradorDTO administradorDTO, IAdministradorServico administradorServico) =>
{
    var valIdacao = new ErrosDeValIdacao
    {
        Mensagens = new List<string>()
    };

    if (string.IsNullOrEmpty(administradorDTO.Email))
        valIdacao.Mensagens.Add("Email não pode ser vazio");


    if (string.IsNullOrEmpty(administradorDTO.Senha))
        valIdacao.Mensagens.Add("Senha não pode ser vazias");


    if (administradorDTO.Perfil == null)
        valIdacao.Mensagens.Add("Perfil não pode ser vazio");


    if (valIdacao.Mensagens.Count > 0)
        return Results.BadRequest(valIdacao);


    var administrador = new Administrador
    {
        Email = administradorDTO.Email,
        Senha = administradorDTO.Senha,
        Perfil = administradorDTO?.Perfil?.ToString() ?? Perfil.Editor.ToString()

    };
    administradorServico.Incluir(administrador);

    return Results.Created($"/Administradores/{administrador.Id}", new AdministradorModelView
    {
        Id = administrador.Id,
        Email = administrador.Email,
        Perfil = administrador.Perfil,

    });


}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
.WithTags("Administradores");
#endregion

#region Veiculos
ErrosDeValIdacao valIdaDTO(VeiculoDTO veiculoDTO)
{
    var valIdacao = new ErrosDeValIdacao
    {
        Mensagens = new List<string>()
    };


    if (string.IsNullOrEmpty(veiculoDTO.Nome))
    {
        valIdacao.Mensagens.Add("O nome não pode ser vazio");
    }

    if (string.IsNullOrEmpty(veiculoDTO.Marca))
    {
        valIdacao.Mensagens.Add("A marca não pode ser vazio");

    }
    if (veiculoDTO.Ano < 1950)
    {
        valIdacao.Mensagens.Add("Veiculo muito antigo, aceito somente anos superiores a 1950.");
    }

    return valIdacao;
}

app.MapPost("/veiculos", ([FromBody] VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
{

    var valIdacao = valIdaDTO(veiculoDTO);
    if (valIdacao.Mensagens.Count > 0)
    {
        return Results.BadRequest(valIdacao);
    }

    var veiculo = new Veiculo
    {
        Nome = veiculoDTO.Nome,
        Marca = veiculoDTO.Marca,
        Ano = veiculoDTO.Ano,

    };
    veiculoServico.Incluir(veiculo);

    return Results.Created($"/veiculo/{veiculo.Id}", veiculo);

}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm,Editor" })
.WithTags("Veiculos");

app.MapGet("/veiculos", ([FromQuery] int? pagina, IVeiculoServico veiculoServico) =>
{

    var veiculos = veiculoServico.Todos(pagina);

    return Results.Ok(veiculos);

}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
.WithTags("Veiculos");


app.MapGet("/veiculos/{Id}", ([FromRoute] int Id, IVeiculoServico veiculoServico) =>
{

    var veiculo = veiculoServico.BuscaPorId(Id);

    if (veiculo == null) return Results.NotFound();

    return Results.Ok(veiculo);

}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm,Editor" })
.WithTags("Veiculos");

app.MapPut("/veiculos/{Id}", ([FromRoute] int Id, VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
{
    var valIdacao = valIdaDTO(veiculoDTO);
    if (valIdacao.Mensagens.Count > 0)
    {
        return Results.BadRequest(valIdacao);
    }


    var veiculo = veiculoServico.BuscaPorId(Id);

    if (veiculo == null) return Results.NotFound();

    veiculo.Nome = veiculoDTO.Nome;
    veiculo.Marca = veiculoDTO.Marca;
    veiculo.Ano = veiculoDTO.Ano;

    veiculoServico.Atualizar(veiculo);

    return Results.Ok(veiculo);
}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
.WithTags("Veiculos");

app.MapDelete("/veiculos/{Id}", ([FromRoute] int Id, IVeiculoServico veiculoServico) =>
{
    var veiculo = veiculoServico.BuscaPorId(Id);

    if (veiculo == null) return Results.NotFound();

    veiculoServico.Apagar(veiculo);

    return Results.NoContent();
}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
.WithTags("Veiculos");

#endregion




#region App
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
#endregion