using System;
using System.Threading.Tasks;
using BusinessLogic.Mesas.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs.Mesas;
using Shared.Exceptions;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/mesas")]
    [Authorize] // Todos los endpoints requieren estar autenticado
    public class MesasController : ControllerBase
    {
        private readonly IMesaService _mesaService;

        // 1. Inyectamos el servicio en el constructor
        public MesasController(IMesaService mesaService)
        {
            _mesaService = mesaService;
        }

        // ==========================================
        // POST /api/mesas — Crear Mesa (CU-21)
        // ==========================================
        [HttpPost]
        [Authorize(Roles = "Admin")] // Solo el Admin puede crear mesas
        public async Task<IActionResult> CrearMesa([FromBody] MesaCreateDTO mesaDto)
        {
            try
            {
                // Llamamos al servicio para crear la mesa
                var mesaCreada = await _mesaService.CreateAsync(mesaDto);

                // Devolvemos 201 Created con la ubicación del nuevo recurso y el objeto creado
                return CreatedAtAction(nameof(CrearMesa), new { id = mesaCreada.Id }, mesaCreada);
            }
            catch (ConflictException ex)
            {
                // HTTP 409: ya existe una mesa con ese número
                return Conflict(new { mensaje = ex.Message });
            }
        }

        // PUT /api/mesas/{id} — Modificar Mesa (CU-22)
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")] // Solo el Admin puede modificar mesas
        public async Task<IActionResult> ModificarMesa(Guid id, [FromBody] MesaUpdateDTO mesaDto)
        {
            // Llamamos al servicio para que intente modificar la mesa
            var resultado = await _mesaService.ModificarMesaAsync(id, mesaDto);

            // Si el servicio devuelve false, significa que no encontró la mesa con ese ID
            if (!resultado)
            {
                return NotFound(new { mensaje = "Mesa no encontrada." }); // HTTP 404
            }

            // Si todo salió bien, devolvemos un 204 No Content
            return NoContent();
        }

        // DELETE /api/mesas/{id} — Eliminar Mesa (CU-23)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")] // Solo el Admin puede eliminar mesas
        public async Task<IActionResult> EliminarMesa(Guid id)
        {
            try
            {
                // 1. Llamamos al servicio para eliminar
                var resultado = await _mesaService.EliminarMesaAsync(id);

                // 2. Si nos devuelve false, es porque no existe
                if (!resultado)
                {
                    return NotFound(new { mensaje = "Mesa no encontrada." }); // HTTP 404
                }

                // 3. Si se borró con éxito, devolvemos un 204
                return NoContent(); // HTTP 204
            }
            catch (ConflictException ex)
            {
                // HTTP 409: la mesa tiene reservas asociadas y no se puede eliminar
                return Conflict(new { mensaje = ex.Message });
            }
        }
    }
}
