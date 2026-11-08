// ═══════════════════════════════════════════════════════════════════════════
//  LocomocionNavMesh.cs — La capa de movimiento
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  MATERIAL DE PARTIDA — ya está terminado. No lo toquéis.
//
//  Este componente es la razón por la que podéis cambiar de FSM a árbol sin
//  que se note: NADIE más en todo el proyecto puede llamar a SetDestination.
//  La decisión escribe «quiero ir a X» en la pizarra y aquí se traduce a
//  movimiento. Si en vuestro código un estado o un nodo hoja llama al
//  NavMeshAgent, habéis roto la capa y lo veré.
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.AI;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class LocomocionNavMesh : MonoBehaviour
    {
        [Tooltip("A qué distancia del destino se considera que ha llegado.")]
        public float ToleranciaLlegada = 0.75f;

        private NavMeshAgent _agente;

        private void Awake()
        {
            _agente = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// Lee el destino de la pizarra y mueve. Escribe de vuelta si ha llegado.
        /// Lo llama GuardiaAgente en la fase 3 del ciclo, todos los frames.
        /// </summary>
        public void Actuar(Blackboard bb)
        {
            // Si el agente no está sobre el NavMesh, cualquier llamada al
            // NavMeshAgent escupe un error rojo por frame. Suele pasar cuando
            // se olvida hornear la superficie: mirad el README.
            if (_agente == null || !_agente.isOnNavMesh) return;

            if (!bb.Get(ClavesBB.TieneDestino, false))
            {
                _agente.isStopped = true;
                bb.Set(ClavesBB.HaLlegado, true);
                return;
            }

            Vector3 destino = bb.Get(ClavesBB.Destino, transform.position);

            // Reordenar la ruta solo si el destino ha cambiado de verdad.
            // SetDestination recalcula el camino: llamarlo cada frame con el
            // mismo valor es tirar CPU (y con 200 agentes se nota, ya lo
            // veréis en P2).
            if ((_agente.destination - destino).sqrMagnitude > 0.01f)
                _agente.SetDestination(destino);

            _agente.isStopped = false;

            bool llegado = !_agente.pathPending &&
                           _agente.remainingDistance <=
                               Mathf.Max(ToleranciaLlegada, _agente.stoppingDistance);

            bb.Set(ClavesBB.HaLlegado, llegado);
        }

        /// <summary>Velocidad actual normalizada 0..1. Útil para el Animator.</summary>
        public float VelocidadNormalizada
        {
            get
            {
                if (_agente == null || _agente.speed <= 0f) return 0f;
                return Mathf.Clamp01(_agente.velocity.magnitude / _agente.speed);
            }
        }
    }
}
