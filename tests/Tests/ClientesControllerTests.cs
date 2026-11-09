using System.Security.Claims;
using Api.Controllers;
using BusinessLogic.Cliente.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shared.DTOs.Clientes;
using Xunit;

namespace Tests;

public class ClientesControllerTests
{
    private readonly Mock<IClienteService> _clienteServiceMock;
    private readonly ClientesController _sut;

    public ClientesControllerTests()
    {
        _clienteServiceMock = new Mock<IClienteService>();
        _sut = new ClientesController(_clienteServiceMock.Object);
    }

    private void SetUserContext(Guid userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetById_ClienteConsultandoSuPropioPerfil_Retorna200Ok()
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        SetUserContext(clienteId, "Cliente");

        var clienteDto = new ClienteResponseDTO
        {
            Id = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            Email = "juan@example.com",
            Telefono = "1122334455"
        };
        _clienteServiceMock.Setup(s => s.GetByIdAsync(clienteId)).ReturnsAsync(clienteDto);

        // Act
        var result = await _sut.GetById(clienteId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDto = Assert.IsType<ClienteResponseDTO>(okResult.Value);
        Assert.Equal(clienteId, returnedDto.Id);
    }

    [Fact]
    public async Task GetById_ClienteConsultandoPerfilAjeno_Retorna403Forbid()
    {
        // Arrange (Prevención de fuga de PII e IDOR)
        var clienteAutenticadoId = Guid.NewGuid();
        var otroClienteId = Guid.NewGuid();
        SetUserContext(clienteAutenticadoId, "Cliente");

        // Act
        var result = await _sut.GetById(otroClienteId);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
        _clienteServiceMock.Verify(s => s.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Gerente")]
    public async Task GetById_GerenteOAdmin_Retorna200OkParaCualquierCliente(string rol)
    {
        // Arrange
        var staffId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        SetUserContext(staffId, rol);

        var clienteDto = new ClienteResponseDTO
        {
            Id = clienteId,
            Nombre = "Carlos",
            Apellido = "López",
            Email = "carlos@example.com",
            Telefono = "1199887766"
        };
        _clienteServiceMock.Setup(s => s.GetByIdAsync(clienteId)).ReturnsAsync(clienteDto);

        // Act
        var result = await _sut.GetById(clienteId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDto = Assert.IsType<ClienteResponseDTO>(okResult.Value);
        Assert.Equal(clienteId, returnedDto.Id);
    }

    [Fact]
    public async Task GetMyProfile_ClienteAutenticado_Retorna200Ok()
    {
        // Arrange
        var clienteId = Guid.NewGuid();
        SetUserContext(clienteId, "Cliente");

        var clienteDto = new ClienteResponseDTO
        {
            Id = clienteId,
            Nombre = "Ana",
            Apellido = "Gómez",
            Email = "ana@example.com",
            Telefono = "1133445566"
        };
        _clienteServiceMock.Setup(s => s.GetByIdAsync(clienteId)).ReturnsAsync(clienteDto);

        // Act
        var result = await _sut.GetMyProfile();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDto = Assert.IsType<ClienteResponseDTO>(okResult.Value);
        Assert.Equal(clienteId, returnedDto.Id);
    }
}

