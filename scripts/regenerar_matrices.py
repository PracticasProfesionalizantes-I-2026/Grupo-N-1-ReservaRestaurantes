# Reemplaza en las filas de las matrices de trazabilidad los nombres de test documentados
# por los nombres reales que existen en tests/**/*.cs. Los que no tienen test equivalente se marcan como pendiente.
import re, glob, os

MAP = {
    "AsignarMesaAsync_WhenCapacityInsufficient_ThrowsCapacidadInsuficienteException": "AsignarMesaAsync_CapacidadInsuficiente_LanzaCapacidadInsuficienteException",
    "AsignarMesaAsync_WhenTableNotFound_ThrowsEntidadNoEncontradaException": "AsignarMesaAsync_MesaInexistente_LanzaNotFoundException",
    "AsignarMesaAsync_WhenTableOverlaps_ThrowsMesaSolapadaException": "AsignarMesaAsync_MesaSolapada_LanzaMesaSolapadaException",
    "AsignarMesaAsync_WithValidTableAndCapacity_AssignsSuccessfully": "AsignarMesaAsync_MesaValida_AsignaCorrectamente",
    "CambiarEstadoMesaAsync_WhenMesaNotFound_ThrowsMesaNoEncontradaException": "CambiarEstadoMesaAsync_CuandoMesaNoExiste_RetornaNull",
    "CambiarEstadoMesaAsync_WhenReservaEnCurso_ThrowsMesaConReservaActivaException": "CambiarEstadoMesaAsync_ALibreConReservaEnCurso_LanzaMesaConReservaActivaException",
    "CambiarEstadoMesaAsync_WithValidState_UpdatesMesaSuccessfully": "CambiarEstadoMesaAsync_ALibreSinReservaEnCurso_ActualizaEstado",
    "CambiarRolUsuarioAsync_WhenLastAdmin_ThrowsOperacionInvalidaException": "CambiarRolUsuarioAsync_QuitarAdminUnicoAdmin_LanzaConflictException",
    "CambiarRolUsuarioAsync_WhenNotFound_ThrowsUsuarioNoEncontradoException": "CambiarRolUsuarioAsync_CuandoUsuarioNoExiste_LanzaNotFoundException",
    "CambiarRolUsuarioAsync_WithValidRole_UpdatesRoleAndRevokesTokens": "CambiarRolUsuarioAsync_ConRevocacion_InvocaRevokeUserTokens",
    "CancelarReservaAsync_WithValidReason_CancelsAndFreesTable": "CancelarReservaGerenteAsync_ReservaEnCurso_CancelaYLiberaMesaYPersisteMotivo",
    "CancelarReservaGerente_WithoutMotivo_Returns400BadRequest": "CancelarReservaGerenteAsync_SinMotivo_LanzaValidationException",
    "CancelarReservaClienteAsync_WhenEnCurso_ThrowsTransicionEstadoInvalidaException": "CancelarReservaClienteAsync_ReservaEnCurso_LanzaTransicionEstadoInvalidaException",
    "CancelarReservaClienteAsync_WhenNotOwner_ThrowsAccesoDenegadoException": "CancelarReservaClienteAsync_ReservaDeOtroCliente_LanzaAccesoDenegadoException",
    "CancelarReservaClienteAsync_WhenOwnerAndPending_CancelsAndFreesMesa": "CancelarReservaClienteAsync_ReservaPendiente_CambiaACancelada",
    "ConfirmarReservaAsync_WhenAlreadyConfirmed_ThrowsTransicionEstadoInvalidaException": "ConfirmarReservaAsync_ReservaYaConfirmada_LanzaTransicionEstadoInvalidaException",
    "ConfirmarReservaAsync_WhenNotFound_ThrowsReservaNoEncontradaException": "ConfirmarReservaAsync_ReservaNoExiste_LanzaReservaNoEncontradaException",
    "ConfirmarReservaAsync_WhenPendingAndTableAssigned_ConfirmsSuccessfully": "ConfirmarReservaAsync_ReservaPendiente_CambiaAConfirmada",
    "CrearMesaAsync_WhenNumeroExists_ThrowsNumeroMesaDuplicadoException": "CreateAsync_CuandoNumeroYaExiste_LanzaConflictException",
    "CrearMesaAsync_WithValidData_CreatesMesaWithLibreState": "CreateAsync_CuandoNumeroNoExiste_RetornaDTO",
    "CreateAsync_WhenEmailExists_ThrowsConflictException": "RegisterAsync_ConEmailExistente_LanzaEmailDuplicadoException",
    "EliminarMesaAsync_WhenHasActiveReservas_ThrowsMesaConReservasActivasException": "EliminarMesaAsync_CuandoTieneReservasActivasOFuturas_LanzaConflictException",
    "EliminarMesaAsync_WhenNotFound_ThrowsMesaNoEncontradaException": "EliminarMesaAsync_CuandoMesaNoExiste_RetornaFalse",
    "EliminarMesaAsync_WithoutActiveReservas_DeletesSuccessfully": "EliminarMesaAsync_CuandoMesaExisteSinReservas_EliminaYRetornaTrue",
    "EliminarUsuarioAsync_SelfDelete_ThrowsOperacionInvalidaException": "EliminarUsuarioAsync_AutoEliminacion_LanzaConflictException",
    "EliminarUsuarioAsync_WhenNotFound_ThrowsUsuarioNoEncontradoException": "EliminarUsuarioAsync_UsuarioNoExiste_LanzaNotFoundException",
    "EliminarUsuarioAsync_WithHistory_PerformsSoftDelete": "EliminarUsuarioAsync_ConReservas_AplicaBajaLogicaYRetornaFalse",
    "FinalizarReservaAsync_WhenEnCurso_TransitionsToFinalizadaAndFreesTable": "FinalizarReservaAsync_ReservaEnCurso_CambiaAFinalizadaYLiberaMesa",
    "FinalizarReservaAsync_WhenNotEnCurso_ThrowsTransicionEstadoInvalidaException": "FinalizarReservaAsync_ReservaPendiente_LanzaTransicionEstadoInvalidaException",
    "GetContextoInicioAsync_ReturnsSummaryAccordingToRole": "GetContextoInicioAsync_Cliente_RetornaResumenClienteYAccesosRapidos",
    "GetMesasFiltradasAsync_WhenNoMatch_ReturnsEmptyList": "FiltrarMapaMesasAsync_CuandoNoHayMesas_RetornaColeccionVacia",
    "GetMesasFiltradasAsync_WithValidFilters_ReturnsFilteredMesas": "FiltrarMapaMesasAsync_CuandoHayMesas_RetornaLista",
    "GetTableroOperativoAsync_ReturnsActiveReservasWithAvailableActions": "GetTableroOperativoAsync_ConReservasOperativas_RetornaDTOs",
    "SolicitarReservaAsync_WithValidData_ReturnsCreatedReserva": "SolicitarReservaAsync_ConDatosValidos_CreaReservaYRetornaDTO",
    "SolicitarReservaAsync_ExistingActiveReserva_ThrowsReservaDuplicadaException": "SolicitarReservaAsync_ClienteConReservaActivaEnMismoTurno_LanzaReservaDuplicadaClienteException",
    "SolicitarReservaAsync_NoTablesAvailable_ThrowsSinDisponibilidadException": "SolicitarReservaAsync_SinMesasConCapacidadSuficiente_LanzaSinDisponibilidadException",
    "SolicitarReservaAsync_OutsideOperatingHours_ThrowsHorarioNoHabilitadoException": "SolicitarReservaAsync_HorarioFueraDeServicio_LanzaHorarioNoHabilitadoException",
    "LoginAsync_WhenUserInactive_ThrowsCuentaInactivaException": "LoginAsync_UsuarioInactivoConCredencialesValidas_LanzaCuentaInactivaException",
    "LoginAsync_WithValidCredentials_ReturnsTokenAndUserClaims": "LoginAsync_CredencialesValidas_RetornaTokenYDatos",
    "LoginAsync_WithWrongPassword_ThrowsCredencialesInvalidasException": "LoginAsync_ContrasenaBCryptIncorrecta_LanzaUnauthorizedException",
    "LogoutAsync_WithValidSession_RevokesRefreshToken": "LogoutAsync_ConTokenYUsuario_InvocaRevocacionDeTokenYUsuario",
    "ModificarMesaAsync_WhenCapacityBelowConfirmed_ThrowsCapacidadIncompatibleException": "ModificarMesaAsync_CapacidadRompeReservaFutura_LanzaConflictException",
    "ModificarMesaAsync_WhenNotFound_ThrowsMesaNoEncontradaException": "ModificarMesaAsync_CuandoMesaNoExiste_RetornaFalse",
    "ModificarMesaAsync_WithValidData_UpdatesMesaSuccessfully": "ModificarMesaAsync_CuandoMesaExiste_ActualizaYRetornaTrue",
    "PromoverSolicitudAsync_WhenNotFound_ThrowsEntidadNoEncontradaException": "PromoverSolicitudAsync_WhenNotFound_ThrowsReservaNoEncontradaException",
    "RechazarReservaAsync_WhenConfirmada_ThrowsTransicionEstadoInvalidaException": "RechazarReservaAsync_ReservaNoPendiente_LanzaTransicionEstadoInvalidaException",
    "RechazarReservaAsync_WhenPendingWithReason_RejectsSuccessfully": "RechazarReservaAsync_ReservaPendiente_CambiaARechazadaYLiberaMesaYPersisteMotivo",
    "RechazarReserva_WithoutMotivo_Returns400BadRequest": "RechazarReservaAsync_SinMotivo_LanzaValidationException",
    "SolicitarReserva_Returns201Created": "SolicitarReserva_Autenticado_RetornaCreated",
    "CancelarReservaCliente_Returns200OK": "CancelarReserva_ClienteDueño_RetornaOk",
    "ConfirmarReserva_Returns200OK": "ConfirmarReserva_Gerente_RetornaOk",
    "ConfirmarReserva_AsCliente_Returns403Forbidden": "ConfirmarReserva_ComoCliente_RetornaForbidden",
    "Login_WithCorrectCredentials_Returns200AndJwtToken": "Login_ConCredencialesValidas_RetornaTokenJWT",
    "Login_WithWrongCredentials_Returns401Unauthorized": "Login_ConCredencialesInvalidas_RetornaUnauthorized",
    "Logout_WithValidAuth_Returns200OK": "Logout_ConTokenValido_RetornaOk",
    "Logout_WithoutToken_Returns401Unauthorized": "Logout_SinToken_RetornaUnauthorized",
    "GetReservasPorFecha_Returns200WithReservasList": "FiltrarPorFecha_ClienteAutenticado_RetornaOk",
    "GetReservasOperativas_AsCliente_Returns403Forbidden": "ListaReservas_ComoCliente_RetornaForbidden",
}

PEND = "_pendiente de test_"
root = os.getcwd()
real = set()
for f in glob.glob(os.path.join(root, "tests", "**", "*.cs"), recursive=True):
    if os.sep + "obj" + os.sep in f or os.sep + "bin" + os.sep in f:
        continue
    for m in re.finditer(r"public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\s*\(", open(f, encoding="utf-8").read()):
        real.add(m.group(1))

bad = [k for k, v in MAP.items() if v not in real]
if bad:
    raise SystemExit(f"Destinos inexistentes en el mapa: {bad}")

stats = {"reasignado": 0, "pendiente": 0}
for f in sorted(glob.glob(os.path.join(root, "Documentacion", "DocumentosNuestros", "cu-*.md"))):
    out = []
    for line in open(f, encoding="utf-8", newline="").read().split("\n"):
        if line.startswith("|"):
            def sub(m):
                name = m.group(1)
                if name in real:
                    return m.group(0)
                if name in MAP:
                    stats["reasignado"] += 1
                    return "`" + MAP[name] + "`"
                stats["pendiente"] += 1
                return PEND
            line = re.sub(r"`([A-Za-z]\w*_\w+)`", sub, line)
        out.append(line)
    open(f, "w", encoding="utf-8", newline="").write("\n".join(out))
print(f"Reasignados: {stats['reasignado']}  |  marcados pendientes: {stats['pendiente']}")
