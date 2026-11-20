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

using UDIT.IA3.BT;
using UnityEngine;
using UnityEngine.AI;

namespace UDIT.IA3.Guardia
{
    public class DecisorBT : IDecisor
    {
        private const float UmbralCambioPersecucion = 0.5f;
        private const float UmbralCambioSospecha = 1.0f;
        private const float EsperaEnPuntoSospecha = 2.0f;
        private const float EsperaTrasPedir = 0.3f;
        private const float RadioBusqueda = 3.0f;
        private const float DuracionBusqueda = 8.0f;

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
            Vector3 ultimoDestinoAlerta = Vector3.zero;
            float ultimoPedidoAlerta = -999f;

            Vector3 ultimoDestinoSospecha = Vector3.zero;
            float pedidoSospechaEn = -999f;
            float llegadaSospechaEn = -1f;
            bool rastreandoSospecha = false;

            float finBusqueda = 0f;
            float pedidoBusquedaEn = -999f;
            Vector3 centroBusqueda = Vector3.zero;

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
                new Monitor("RamaAlerta",
                    new Condition("Nivel==Alerta", bb =>
                        bb.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo) == NivelSospecha.Alerta),
                    new ActionNode("Perseguir", bb =>
                    {
                        Vector3 objetivoPos = bb.Get(ClavesBB.UltimaPos, Vector3.zero);
                        bool necesitaDestino =
                            !bb.Get(ClavesBB.TieneDestino, false) ||
                            (objetivoPos - ultimoDestinoAlerta).sqrMagnitude >
                            UmbralCambioPersecucion * UmbralCambioPersecucion;

                        if (necesitaDestino && bb.Tiempo - ultimoPedidoAlerta > 0.15f)
                        {
                            ultimoDestinoAlerta = objetivoPos;
                            ultimoPedidoAlerta = bb.Tiempo;
                            bb.Set(ClavesBB.Destino, objetivoPos);
                            bb.Set(ClavesBB.TieneDestino, true);
                        }

                        return Status.Running;
                    })
                ),

                // ── TODO · rama de SOSPECHA (investigar) ────────────────────
                new Monitor("RamaSospecha",
                    new Condition("Nivel==Sospecha", bb =>
                        bb.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo) == NivelSospecha.Sospecha),
                    new ActionNode("Investigar", bb =>
                    {
                        Vector3 puntoActual = bb.Has(ClavesBB.PosSospecha)
                            ? bb.Get(ClavesBB.PosSospecha, Vector3.zero)
                            : bb.Get(ClavesBB.UltimaPos, Vector3.zero);

                        if (!rastreandoSospecha &&
                            (!bb.Get(ClavesBB.TieneDestino, false) ||
                             (puntoActual - ultimoDestinoSospecha).sqrMagnitude >
                             UmbralCambioSospecha * UmbralCambioSospecha))
                        {
                            ultimoDestinoSospecha = puntoActual;
                            pedidoSospechaEn = bb.Tiempo;
                            llegadaSospechaEn = -1f;
                            bb.Set(ClavesBB.Destino, puntoActual);
                            bb.Set(ClavesBB.TieneDestino, true);
                            return Status.Running;
                        }

                        bool haLlegado = bb.Get(ClavesBB.HaLlegado, false) &&
                                         (bb.Tiempo - pedidoSospechaEn > EsperaTrasPedir);

                        if (!rastreandoSospecha)
                        {
                            if (!haLlegado)
                            {
                                llegadaSospechaEn = -1f;
                                return Status.Running;
                            }

                            if (llegadaSospechaEn < 0f)
                                llegadaSospechaEn = bb.Tiempo;
                            else if (bb.Tiempo - llegadaSospechaEn >= EsperaEnPuntoSospecha)
                            {
                                rastreandoSospecha = true;
                                ultimoDestinoSospecha = puntoActual;
                                pedidoBusquedaEn = -999f;
                            }

                            return Status.Running;
                        }

                        // Tras esperar en el punto, rastrea alrededor igual que EstadoInvestigar -> EstadoBuscar
                        if (haLlegado && bb.Tiempo - pedidoBusquedaEn > EsperaTrasPedir)
                        {
                            Vector3 candidato = PuntoCercanoNavMesh(ultimoDestinoSospecha, RadioBusqueda);
                            bb.Set(ClavesBB.Destino, candidato);
                            bb.Set(ClavesBB.TieneDestino, true);
                            pedidoBusquedaEn = bb.Tiempo;
                        }

                        return Status.Running;
                    })
                ),

                // ── TODO · rama de BÚSQUEDA (última posición conocida) ──────
                new Sequence("RamaBusqueda",
                    new Condition("TieneUltimaPos?", bb =>
                        bb.Get(ClavesBB.TieneUltimaPos, false)),
                    new ActionNode("BuscarAlrededor", bb =>
                    {
                        if (finBusqueda <= bb.Tiempo && !bb.Get(ClavesBB.TieneDestino, false))
                        {
                            centroBusqueda = bb.Get(ClavesBB.UltimaPos, _agente.transform.position);
                            finBusqueda = bb.Tiempo + DuracionBusqueda;
                            bb.Set(ClavesBB.Destino, centroBusqueda);
                            bb.Set(ClavesBB.TieneDestino, true);
                            pedidoBusquedaEn = bb.Tiempo;
                        }

                        // Si el nivel volvió a subir a Sospecha o Alerta, cede el control a ramas superiores
                        NivelSospecha nivel = bb.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo);
                        if (nivel != NivelSospecha.Tranquilo)
                            return Status.Failure;

                        if (bb.Tiempo >= finBusqueda)
                        {
                            finBusqueda = 0f;
                            bb.Set(ClavesBB.TieneUltimaPos, false);
                            return Status.Success;
                        }

                        bool llegado = bb.Get(ClavesBB.HaLlegado, false);
                        if (llegado && bb.Tiempo - pedidoBusquedaEn > EsperaTrasPedir)
                        {
                            Vector3 siguiente = PuntoCercanoNavMesh(centroBusqueda, RadioBusqueda);
                            bb.Set(ClavesBB.Destino, siguiente);
                            bb.Set(ClavesBB.TieneDestino, true);
                            pedidoBusquedaEn = bb.Tiempo;
                        }

                        return Status.Running;
                    })
                ),

                // ── Rama de PATRULLA — ejemplo de nodo hoja enchufado ───────
                new ActionNode("Patrullar", bb =>
                {
                    ultimoDestinoAlerta = Vector3.zero;
                    ultimoDestinoSospecha = Vector3.zero;
                    llegadaSospechaEn = -1f;
                    rastreandoSospecha = false;
                    finBusqueda = 0f;

                    RutaPatrulla ruta = _agente.GetComponent<RutaPatrulla>();
                    if (ruta == null || !ruta.TieneRuta) return Status.Failure;

                    // Si ya llegué al punto anterior, pido el siguiente.
                    if (bb.Get(ClavesBB.HaLlegado, false))
                    {
                        bb.Set(ClavesBB.Destino, ruta.Avanzar());
                        bb.Set(ClavesBB.TieneDestino, true);
                    }
                    else if (bb.Get(ClavesBB.HaLlegado, false))
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
        private static Vector3 PuntoCercanoNavMesh(Vector3 centro, float radio)
        {
            Vector2 circulo = Random.insideUnitCircle * radio;
            Vector3 candidato = centro + new Vector3(circulo.x, 0f, circulo.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidato, out hit, radio, NavMesh.AllAreas))
                return hit.position;

            return centro;
        }
    }
}
