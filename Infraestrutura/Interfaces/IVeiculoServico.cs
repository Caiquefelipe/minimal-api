using minimal_api.Dominio.DTOs;
using minimal_api.Dominio.EntIdades;

namespace minimal_api.Infraestrutura.Interfaces
{
    public interface IVeiculoServico
    {
        List<Veiculo>? Todos(int? pagina, string? nome = null, string? marca = null);
        Veiculo? BuscaPorId(int Id);
        void Incluir(Veiculo veiculo);

        void Atualizar(Veiculo veiculo);

        void Apagar(Veiculo veiculo);
    }
}
