using System.ComponentModel.DataAnnotations;
using BusinessLogic.Cliente.Implementations;
using DataAccess.Entities;
using DataAccess.Repositories.Interfaces;
using Moq;
using Shared.DTOs.Clientes;
using Shared.Exceptions;

namespace Tests;

/// <summary>
/// Pruebas unitarias de validación y lógica de negocio para ClienteService y ClienteCreateDTO.
/// </summary>
public class ClienteServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly ClienteService _sut;

    public ClienteServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _sut = new ClienteService(_clienteRepoMock.Object);
    }

    [Theory]
    [InlineData("114455hola")]
    [InlineData("abc")]
    [InlineData("telefono123")]
    [InlineData("12345a6789")]
    [InlineData("+54 11 abc-6677")]
    [InlineData("palabra")]
    [InlineData("11-22-ABCD")]
    public async Task CreateAsync_ConTelefonoConLetras_LanzaValidationException(string telefonoInvalido)
    {
        // Arrange
        var dto = new ClienteCreateDTO
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Telefono = telefonoInvalido,
            Password = "Password123!"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Shared.Exceptions.ValidationException>(() => _sut.CreateAsync(dto));
        Assert.Contains("teléfono", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_ConTelefonoVacioOEspacios_LanzaValidationException(string telefonoVacio)
    {
        // Arrange
        var dto = new ClienteCreateDTO
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Telefono = telefonoVacio,
            Password = "Password123!"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Shared.Exceptions.ValidationException>(() => _sut.CreateAsync(dto));
        Assert.Contains("teléfono", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1198765432")]
    [InlineData("+54 11 4455-6677")]
    [InlineData("+5491122334455")]
    [InlineData("(011) 4455-6677")]
    [InlineData("011-4455-6677")]
    public async Task CreateAsync_ConTelefonoValido_CreaClienteExitosamente(string telefonoValido)
    {
        // Arrange
        var dto = new ClienteCreateDTO
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Telefono = telefonoValido,
            Password = "Password123!"
        };

        _clienteRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), null))
                        .ReturnsAsync(false);

        _clienteRepoMock.Setup(r => r.CreateAsync(It.IsAny<Cliente>()))
                        .ReturnsAsync((Cliente c) => c);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dto.Nombre, result.Nombre);
        Assert.Equal(telefonoValido.Trim(), result.Telefono);
        _clienteRepoMock.Verify(r => r.CreateAsync(It.IsAny<Cliente>()), Times.Once);
    }

    [Theory]
    [InlineData("114455hola")]
    [InlineData("abc")]
    [InlineData("telefono123")]
    [InlineData("+54 11 abc-6677")]
    public void ClienteCreateDTO_DataAnnotation_ConTelefonoConLetras_FallaValidacion(string telefonoInvalido)
    {
        // Arrange
        var dto = new ClienteCreateDTO
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Telefono = telefonoInvalido,
            Password = "Password123!"
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(dto);

        // Act
        bool isValid = Validator.TryValidateObject(dto, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(ClienteCreateDTO.Telefono)));
    }

    [Theory]
    [InlineData("1198765432")]
    [InlineData("+54 11 4455-6677")]
    [InlineData("+5491122334455")]
    [InlineData("(011) 4455-6677")]
    public void ClienteCreateDTO_DataAnnotation_ConTelefonoValido_PasaValidacion(string telefonoValido)
    {
        // Arrange
        var dto = new ClienteCreateDTO
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Telefono = telefonoValido,
            Password = "Password123!"
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(dto);

        // Act
        bool isValid = Validator.TryValidateObject(dto, context, validationResults, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
    }
}
