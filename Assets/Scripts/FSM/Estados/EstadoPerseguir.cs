// EstadoPerseguir.cs — Alerta: el guardia va a la última posición conocida.
// No usa la posición real del jugador (eso sería hacer trampa): usa lo que la
// percepción ha escrito en la pizarra.

using UnityEngine;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    public class EstadoPerseguir : State
    {
        private const float UmbralCambioDestino = 0.5f;

        private Vector3 _ultimoPedido;

        public EstadoPerseguir(StateMachine machine) : base(machine) { }

        internal override void Enter()
        {
            Debug.Log("[FSM] Perseguir");
            _ultimoPedido = Pizarra.Get(ClavesBB.UltimaPos, Vector3.zero);
            PedirDestino(_ultimoPedido);
        }

        internal override void Execute()
        {
            NivelSospecha nivel = Pizarra.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo);

            if (nivel != NivelSospecha.Alerta)
            {
                StateMachine.ChangeState(new EstadoBuscar(StateMachine));
                return;
            }

            Vector3 punto = Pizarra.Get(ClavesBB.UltimaPos, Vector3.zero);
            if ((punto - _ultimoPedido).sqrMagnitude > UmbralCambioDestino * UmbralCambioDestino)
            {
                _ultimoPedido = punto;
                PedirDestino(punto);
            }
        }

        private void PedirDestino(Vector3 punto)
        {
            Pizarra.Set(ClavesBB.Destino, punto);
            Pizarra.Set(ClavesBB.TieneDestino, true);
        }
    }
}