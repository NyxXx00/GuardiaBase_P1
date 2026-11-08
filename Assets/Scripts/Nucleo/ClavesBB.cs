// ═══════════════════════════════════════════════════════════════════════════
//  ClavesBB.cs — El contrato del blackboard
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  MATERIAL DE PARTIDA — no hace falta que lo modifiquéis (podéis AÑADIR).
//
//  Estas constantes son el contrato entre las capas. Si escribís las claves
//  como cadenas sueltas por ahí, un día pondréis "ultimaPos" en el sensor y
//  "UltimaPos" en el árbol, y os pasaréis media tarde buscándolo.
// ═══════════════════════════════════════════════════════════════════════════

namespace UDIT.IA3.Guardia
{
    /// <summary>
    /// Las claves del blackboard del guardia. Son las siete de la sesión 5.
    ///
    /// Quién escribe cada una está en el comentario: respetadlo. La regla del
    /// curso es «la percepción escribe, la decisión lee», y estas etiquetas
    /// son la forma de comprobar si la estáis cumpliendo.
    /// </summary>
    public static class ClavesBB
    {
        // ── Las escribe la PERCEPCIÓN ──────────────────────────────────────

        /// <summary>bool · ¿tengo al objetivo a la vista AHORA MISMO?</summary>
        public const string Visible = "visible";

        /// <summary>float 0..1 · cuánto sospecho. Continuo, con decaimiento.</summary>
        public const string Sospecha = "sospecha";

        /// <summary>NivelSospecha · el tramo actual, ya con histéresis aplicada.</summary>
        public const string Nivel = "nivel";

        /// <summary>Vector3 · dónde vi al objetivo por última vez.</summary>
        public const string UltimaPos = "ultimaPos";

        /// <summary>bool · ¿ese recuerdo sigue siendo válido?</summary>
        public const string TieneUltimaPos = "tieneUltimaPos";

        /// <summary>int · cuántas veces lo he perdido. Sube el nervio.</summary>
        public const string VecesPerdido = "vecesPerdido";

        // ── Las escribe la DECISIÓN, las lee el MOVIMIENTO ─────────────────

        /// <summary>Vector3 · a dónde quiero ir. La decisión NO mueve: pide.</summary>
        public const string Destino = "destino";

        /// <summary>bool · ¿hay un destino pendiente?</summary>
        public const string TieneDestino = "tieneDestino";

        // ── Las escribe el MOVIMIENTO, las lee la DECISIÓN ─────────────────

        /// <summary>bool · ¿he llegado ya al destino que me pidieron?</summary>
        public const string HaLlegado = "haLlegado";

        // ── Opcional (§7 del enunciado): blackboard compartido ─────────────

        /// <summary>bool · alerta de grupo. La escribe OTRO guardia, con latencia.</summary>
        public const string AlertaDeGrupo = "alertaDeGrupo";
    }
}
