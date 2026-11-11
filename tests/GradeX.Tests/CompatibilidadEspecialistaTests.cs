using GradeX.Application.Asignaciones;

namespace GradeX.Tests;

public sealed class CompatibilidadEspecialistaTests
{
    [Theory]
    [InlineData(1, null, 1, null, true)]
    [InlineData(1, 5, 1, null, true)]
    [InlineData(1, 5, 1, 5, true)]
    [InlineData(1, null, 1, 5, false)]
    [InlineData(1, 5, 1, 6, false)]
    [InlineData(1, null, 2, null, false)]
    public void ReglaDeCategoriaMarcaEsExplicita(
        int categoriaSolicitud,
        int? marcaSolicitud,
        int categoriaEspecialista,
        int? marcaEspecialista,
        bool esperado)
    {
        var resultado = CompatibilidadEspecialista.EsCompatible(
            categoriaSolicitud, marcaSolicitud, categoriaEspecialista, marcaEspecialista);

        Assert.Equal(esperado, resultado);
    }
}
