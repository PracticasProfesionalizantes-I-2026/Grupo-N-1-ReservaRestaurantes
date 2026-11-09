using BusinessLogic.Mesas.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Mesas;
using Shared.Enums;
using Shared.Exceptions;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Route("api/mesas")]
    [Authorize] // Todos los endpoints requieren estar autenticado
    public class MesasController : ControllerBase
    {
        private readonly IMesaService _mesaService;

        public MesasController(IMesaService mesaService)
        {
            _mesaService = mesaService;
        }

        // ==========================================
        // GET /api/mesas/{id} — Obtener Mesa por ID
        // ==========================================
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(MesaResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var mesa = await _mesaService.GetByIdAsync(id);
            if (mesa == null)
            {
                throw new MesaNoEncontradaException(id);
            }
            return Ok(mesa);
        }

        // ==========================================
        // POST /api/mesas — Crear Mesa (CU-21)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")] // Solo el Admin puede crear mesas
        [ProducesResponseType(typeof(MesaResponseDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CrearMesa([FromBody] MesaCreateDTO mesaDto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var mesaCreada = await _mesaService.CreateAsync(mesaDto);
            return CreatedAtAction(nameof(GetById), new { id = mesaCreada.Id }, mesaCreada);
        }

        // PUT /api/mesas/{id} — Modificar Mesa (CU-22)
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")] // Solo el Admin puede modificar mesas
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ModificarMesa(Guid id, [FromBody] MesaUpdateDTO mesaDto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var resultado = await _mesaService.ModificarMesaAsync(id, mesaDto);

            if (!resultado)
            {
                throw new MesaNoEncontradaException(id);
            }

            return NoContent();
        }

        // DELETE /api/mesas/{id} — Eliminar Mesa (CU-23)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")] // Solo el Admin puede eliminar mesas
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> EliminarMesa(Guid id)
        {
            var resultado = await _mesaService.EliminarMesaAsync(id);

            if (!resultado)
            {
                throw new MesaNoEncontradaException(id);
            }

            return NoContent();
        }

        // PATCH /api/mesas/{id}/estado — Cambiar Estado Mesa (CU-03)
        [HttpPatch("{id:guid}/estado")]
        [Authorize(Roles = "Gerente,Admin")] // RN-01: Rol Gerente o Admin
        [ProducesResponseType(typeof(MesaResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CambiarEstadoMesa(Guid id, [FromBody] MesaCambiarEstadoDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var resultado = await _mesaService.CambiarEstadoMesaAsync(id, dto.NuevoEstado);

            if (resultado == null)
            {
                throw new MesaNoEncontradaException(id);
            }

            return Ok(resultado);
        }

        /// <summary>
        /// CU-10: Filtrar mapa de mesas (Gerente o Admin).
        /// </summary>
        [HttpGet("filtrar-mapa-de-mesas")]
        [Authorize(Roles = "Gerente,Admin")]
        [ProducesResponseType(typeof(IEnumerable<MesaResponseDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> FiltrarMapaMesas([FromQuery] MesaFiltroDTO filtro)
        {
            var result = await _mesaService.FiltrarMapaMesasAsync(filtro);
            return Ok(result);
        }
    }
}
