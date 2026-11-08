// ═══════════════════════════════════════════════════════════════════════════
//  Contratos.cs — Las interfaces que separan las capas
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  MATERIAL DE PARTIDA — no lo modifiquéis.
//
//  Estas dos interfaces son las que hacen posible el apartado D del enunciado:
//  cambiar la FSM por el árbol sin tocar percepción ni locomoción. Si las
//  respetáis, el cambio es una línea. Si las rompéis, es una tarde.
// ═══════════════════════════════════════════════════════════════════════════

using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    /// <summary>
    /// Un sensor. Mira el mundo y ESCRIBE en la pizarra. No decide nada.
    ///
    /// Si alguna vez os apetece que un sensor «decida» algo, es señal de que
    /// la información que produce está mal elegida.
    /// </summary>
    public interface ISensor
    {
        /// <summary>Se llama una vez por ciclo, antes de decidir.</summary>
        void Percibir(Blackboard bb);
    }

    /// <summary>
    /// Un cerebro. LEE la pizarra y escribe INTENCIONES (a dónde quiero ir,
    /// qué quiero hacer). No mira el mundo y no mueve nada.
    ///
    /// Tendréis dos implementaciones —DecisorFSM y DecisorBT— y el agente
    /// tiene que funcionar igual con cualquiera de las dos. Ese es el examen
    /// de verdad de si habéis separado bien las capas.
    /// </summary>
    public interface IDecisor
    {
        /// <summary>Se llama una vez, al arrancar. Aquí se monta la FSM o el árbol.</summary>
        void Iniciar(Blackboard bb);

        /// <summary>Un ciclo de decisión.</summary>
        void Decidir(Blackboard bb);

        /// <summary>Qué ha hecho en el último ciclo. Para el HUD de depuración.</summary>
        string Traza { get; }
    }
}
