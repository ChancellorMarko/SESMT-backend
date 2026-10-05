using SESMT.Domain.Modules.Workforce.Entities;
using SESMT.Domain.ValueObjects;

public class WorkerNameTests
{
    /// <summary>
    /// Garante que espaços externos não façam parte do valor armazenado
    /// pelo Value Object.
    /// </summary>
    [Fact]
    public void Constructor_ShouldNormalizeValue()
    {
        var name = new WorkerName("  João da Silva  ");

        Assert.Equal("João da Silva", name.Value);
    }

    /// <summary>
    /// Garante que o domínio rejeite um nome sem conteúdo válido.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrow_WhenValueIsEmpty()
    {
        Assert.Throws<ArgumentException>(
            () => new WorkerName(" "));
    }

    /// <summary>
    /// Garante que a igualdade do Value Object seja baseada no valor,
    /// independentemente da diferença entre letras maiúsculas e minúsculas.
    /// </summary>
    [Fact]
    public void Equals_ShouldReturnTrue_WhenValuesAreEqualIgnoringCase()
    {
        var first = new WorkerName("João da Silva");
        var second = new WorkerName("JOÃO DA SILVA");

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Garante que o agregado Worker utilize o Value Object
    /// para representar o nome do trabalhador.
    /// </summary>
    [Fact]
    public void Worker_ShouldUseWorkerNameValueObject()
    {
        var functionId = Guid.NewGuid();

        var worker = new Worker(
            functionId,
            "João da Silva",
            new DateTime(2026, 1, 1));

        Assert.Equal(
            "João da Silva",
            worker.Name.Value);
    }
}