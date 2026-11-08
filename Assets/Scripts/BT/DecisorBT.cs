// ═══════════════════════════════════════════════════════════════════════════
//  DecisorBT.cs — Cerebro versión 2: árbol de comportamiento
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  APARTADO C DEL ENUNCIADO.
//
//  El runtime (BehaviorTree.cs) OS LO DOY. No lo modifiquéis: en la sesión 9
//  lo explicamos línea a línea y en la memoria os preguntaré por él.
//
//  Lo que escribís vosotros es lo de este fichero: LA FORMA DEL ÁRBOL y LOS
//  NODOS HOJA. Eso es lo que se evalúa.
//
//  ⚠️ La condición del apartado D: este decisor tiene que producir el MISMO
//  comportamiento observable que DecisorFSM, leyendo la MISMA pizarra, sin que
//  haga falta tocar ni SensorVision ni LocomocionNavMesh. Si para que funcione
//  el árbol necesitáis cambiar algo de la percepción, algo está mal colocado.
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    public class DecisorBT : IDecisor
    {
        private readonly GuardiaAgente _agente;
        private BehaviorTree _arbol;

        public DecisorBT(GuardiaAgente agente) { _agente = agente; }

        public void Iniciar(Blackboard bb)
        {
            _arbol = new BehaviorTree(ConstruirArbol(), bb);
        }

        public void Decidir(Blackboard bb)
        {
            if (_arbol != null) _arbol.Tick();
        }

        public string Traza { get { return "BT: " + Trazador.Ultima(); } }

        // ───────────────────────────────────────────────────────────────────
        //  EL ÁRBOL — ESTO ES VUESTRO
        // ───────────────────────────────────────────────────────────────────

        private Node ConstruirArbol()
        {
            // Lo que hay aquí abajo es un ÁRBOL DE JUGUETE: solo sabe patrullar.
            // Sirve para que el proyecto compile y arranque, y para que veáis
            // cómo se enchufa un nodo hoja. Nada más.
            //
            // Vuestro árbol tiene que cubrir los cuatro comportamientos del §3
            // del enunciado. La forma canónica, la que vimos en la sesión 8, es
            // un ActiveSelector con las ramas ORDENADAS POR PRIORIDAD:
            //
            //   ActiveSelector "Guardia"
            //     ├── Monitor  guarda: ¿nivel == Alerta?     → Perseguir
            //     ├── Monitor  guarda: ¿nivel == Sospecha?   → Investigar
            //     ├── Sequence ¿tengo última posición?       → Buscar
            //     └── Patrullar
            //
            // TRES AVISOS, por orden de cuánta gente tropieza con ellos:
            //
            //  1. Usad ActiveSelector, no Selector. Un Selector normal se queda
            //     pegado al hijo que devolvió Running y no vuelve a mirar si una
            //     rama de mayor prioridad se ha vuelto posible. Con Selector, el
            //     guardia termina la patrulla ANTES de reaccionar a veros.
            //
            //  2. Usad Monitor, no Sequence, cuando la condición tenga que
            //     seguir cumpliéndose durante toda la acción. Una Sequence con
            //     la condición delante NO la vuelve a comprobar: cuando está
            //     Running retoma en la acción. El resultado es un guardia que
            //     persigue eternamente a un jugador al que ya no ve.
            //     Probadlo: cambiad un Monitor por una Sequence y mirad la traza
            //     con "Mostrar Traza" activado. Ese experimento es material de
            //     memoria.
            //
            //  3. Un nodo hoja NO guarda «en qué estado estoy». Si os pilláis
            //     escribiendo un bool _persiguiendo dentro de un nodo, habéis
            //     hecho una FSM disfrazada de árbol, y eso está en la lista de
            //     errores que cuestan nota.

            return new ActiveSelector("Guardia",

                // ── TODO · rama de ALERTA (perseguir) ───────────────────────
                // new Monitor("SiAlerta",
                //     new Condition("¿Alerta?", bb =>
                //         bb.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo) == NivelSospecha.Alerta),
                //     new IrA(...)),

                // ── TODO · rama de SOSPECHA (investigar) ────────────────────

                // ── TODO · rama de BÚSQUEDA (última posición conocida) ──────

                // ── Rama de PATRULLA — ejemplo de nodo hoja enchufado ───────
                new ActionNode("Patrullar", bb =>
                {
                    RutaPatrulla ruta = _agente.GetComponent<RutaPatrulla>();
                    if (ruta == null || !ruta.TieneRuta) return Status.Failure;

                    // Si ya llegué al punto anterior, pido el siguiente.
                    if (bb.Get(ClavesBB.HaLlegado, false))
                    {
                        bb.Set(ClavesBB.Destino, ruta.Avanzar());
                        bb.Set(ClavesBB.TieneDestino, true);
                    }

                    // Patrullar no «termina» nunca: es una acción de fondo.
                    // Por eso devuelve Running y no Success. Si devolviera
                    // Success, el ActiveSelector la daría por hecha y el árbol
                    // se quedaría sin nada que hacer.
                    return Status.Running;
                })
            );
        }
    }
}
