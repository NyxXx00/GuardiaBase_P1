// EstadoBuscar.cs — Ha perdido al jugador: va a la última posición conocida y
// rastrea alrededor durante un tiempo. Si no encuentra nada, olvida y patrulla.

using UnityEngine;
using UnityEngine.AI;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    public class EstadoBuscar : State
    {
        private const float DuracionBusqueda = 8f; 
        private const float RadioBusqueda = 3f;      
        private const float EsperaTrasPedir = 0.3f; 
        private Vector3 _centro;
        private readonly Vector3? _centroInicial;
        private float _fin;
        private float _pedidoEn;

        public EstadoBuscar(StateMachine machine, Vector3? centro = null) : base(machine)
        {
            _centroInicial = centro;
        }

        internal override void Enter()
        {
            Debug.Log("[FSM] Buscar");
            _centro = _centroInicial.HasValue
                ? _centroInicial.Value
                : Pizarra.Get(ClavesBB.UltimaPos, Vector3.zero);
            _fin = Pizarra.Tiempo + DuracionBusqueda;
            PedirDestino(_centro);
        }

        internal override void Execute()
        {
            NivelSospecha nivel = Pizarra.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo);

            if (nivel == NivelSospecha.Alerta)
            {
                StateMachine.ChangeState(new EstadoPerseguir(StateMachine));
                return;
            }

            if (Pizarra.Tiempo >= _fin)
            {
                Pizarra.Set(ClavesBB.TieneUltimaPos, false);
                StateMachine.ChangeState(new EstadoPatrullar(StateMachine));
                return;
            }

            bool llegado = Pizarra.Get(ClavesBB.HaLlegado, false);
            if (llegado && Pizarra.Tiempo - _pedidoEn > EsperaTrasPedir)
                PedirDestino(PuntoCercano(_centro));
        }

        private Vector3 PuntoCercano(Vector3 centro)
        {
            Vector2 c = Random.insideUnitCircle * RadioBusqueda;
            Vector3 candidato = centro + new Vector3(c.x, 0f, c.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidato, out hit, RadioBusqueda, NavMesh.AllAreas))
                return hit.position;
            return centro;
        }

        private void PedirDestino(Vector3 punto)
        {
            Pizarra.Set(ClavesBB.Destino, punto);
            Pizarra.Set(ClavesBB.TieneDestino, true);
            _pedidoEn = Pizarra.Tiempo;
        }
    }
}