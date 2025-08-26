using minimal_api.Dominio.DTOs;
using minimal_api.Dominio.EntIdades;

namespace minimal_api.Infraestrutura.Interfaces
{
    public interface IAdministradorServico
    {
       Administrador? Login(LoginDTO loginDTO);
       Administrador? Incluir(Administrador administrador);

        List<Administrador>  Todos(int? pagina);

        Administrador? BuscaPorId(int id);
    }


}
