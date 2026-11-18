// EstadoInvestigar.cs — El guardia sospecha: se acerca al punto donde notó algo,
// mira un momento y, si no ve nada, pasa a rastrear la zona (EstadoBuscar).
// Solo LEE la pizarra y pide destinos.

using UnityEngine;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    public class EstadoInvestigar : State
    {
        private const float UmbralCambioDestino = 1.0f;
        private const float EsperaEnPunto = 2.0f;
        private const float EsperaTrasPedir = 0.5f;

        private Vector3 _ultimoPedido;
        private float _pedidoEn;
        private float _llegadaEn = -1f;

        public EstadoInvestigar(StateMachine machine) : base(machine) { }

        internal override void Enter()
        {
            Debug.Log("[FSM] Investigar");
            _ultimoPedido = PuntoSospechoso();
            PedirDestino(_ultimoPedido);
        }

        internal override void Execute()
        {
            NivelSospecha nivel = Pizarra.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo);

            if (nivel == NivelSospecha.Alerta)
            {
                StateMachine.ChangeState(new EstadoPerseguir(StateMachine));
                return;
            }

            if (nivel == NivelSospecha.Tranquilo)
            {
                StateMachine.ChangeState(new EstadoPatrullar(StateMachine));
                return;
            }

            // Sigue en Sospecha. Si el punto sospechoso se ha movido, se va hacia él.
            Vector3 punto = PuntoSospechoso();
            if ((punto - _ultimoPedido).sqrMagnitude > UmbralCambioDestino * UmbralCambioDestino)
            {
                _ultimoPedido = punto;
                PedirDestino(punto);
                _llegadaEn = -1f;
                return;
            }

            // ¿Ha llegado? Espera un poco y, si no ha pasado nada, rastrea la zona
            // en lugar de quedarse parado hasta que se le pase la sospecha.
            bool llegado = Pizarra.Get(ClavesBB.HaLlegado, false)
                           && Pizarra.Tiempo - _pedidoEn > EsperaTrasPedir;

            if (!llegado) { _llegadaEn = -1f; return; }

            if (_llegadaEn < 0f) _llegadaEn = Pizarra.Tiempo;
            else if (Pizarra.Tiempo - _llegadaEn >= EsperaEnPunto)
                StateMachine.ChangeState(new EstadoBuscar(StateMachine, punto));
        }

        private Vector3 PuntoSospechoso()
        {
            if (Pizarra.Has(ClavesBB.PosSospecha))
                return Pizarra.Get(ClavesBB.PosSospecha, Vector3.zero);
            return Pizarra.Get(ClavesBB.UltimaPos, Vector3.zero);
        }

        private void PedirDestino(Vector3 punto)
        {
            Pizarra.Set(ClavesBB.Destino, punto);
            Pizarra.Set(ClavesBB.TieneDestino, true);
            _pedidoEn = Pizarra.Tiempo;
        }
    }
}