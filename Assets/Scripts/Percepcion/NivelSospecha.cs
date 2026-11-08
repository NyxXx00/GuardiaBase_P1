// ═══════════════════════════════════════════════════════════════════════════
//  NivelSospecha.cs
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  MATERIAL DE PARTIDA.
// ═══════════════════════════════════════════════════════════════════════════

namespace UDIT.IA3.Guardia
{
    /// <summary>
    /// Los tres tramos del medidor de sospecha.
    ///
    /// OJO a por qué esto es un enum de TRES valores y no un bool: el tramo
    /// intermedio —«creo que he visto algo»— es donde vive el juego. Es lo
    /// que separa un guardia de un interruptor.
    /// </summary>
    public enum NivelSospecha
    {
        /// <summary>No pasa nada. Patrulla.</summary>
        Tranquilo,

        /// <summary>Algo he visto. Investiga, pero sin comprometerse.</summary>
        Sospecha,

        /// <summary>Confirmado. Persigue.</summary>
        Alerta
    }
}
