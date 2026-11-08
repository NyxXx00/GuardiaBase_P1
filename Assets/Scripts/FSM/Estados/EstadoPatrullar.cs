// ═══════════════════════════════════════════════════════════════════════════
//  EstadoPatrullar.cs — EJEMPLO COMPLETO del patrón Estado
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  Éste OS LO DOY ENTERO, para que veáis la forma. Los otros tres estados
//  —Investigar, Perseguir, Buscar— los escribís vosotros copiando esta
//  estructura.
//
//  Fijaos en las tres cosas que hace y en las tres que NO hace:
//
//    SÍ:  lee la pizarra · escribe intenciones (destino) · transita
//    NO:  mirar el mundo (eso es del sensor)
//         llamar al NavMeshAgent (eso es de LocomocionNavMesh)
//         guardar «en qué estado estoy» en la pizarra (para eso está la FSM)
// ═══════════════════════════════════════════════════════════════════════════

namespace UDIT.IA3.Guardia
{
    public class EstadoPatrullar : State
    {
        public EstadoPatrullar(StateMachine machine) : base(machine) { }

        internal override void Enter()
        {
            // Al entrar, apuntamos al punto de patrulla que toque.
            RutaPatrulla ruta = Agente.GetComponent<RutaPatrulla>();

            if (ruta != null && ruta.TieneRuta)
            {
                Pizarra.Set(ClavesBB.Destino, ruta.PuntoActual);
                Pizarra.Set(ClavesBB.TieneDestino, true);
            }
            else
            {
                // Sin ruta configurada nos quedamos quietos, pero avisamos:
                // es un fallo de montaje de escena, no de código.
                Pizarra.Set(ClavesBB.TieneDestino, false);
                UnityEngine.Debug.LogWarning(
                    "[" + Agente.name + "] no tiene RutaPatrulla con puntos. " +
                    "Mirad el paso 6 del README_Montaje.", Agente);
            }
        }

        internal override void Execute()
        {
            // ── 1. ¿Hay razón para dejar de patrullar? ──────────────────────
            //
            // Se pregunta a la PIZARRA, nunca al sensor. El estado no sabe si
            // la sospecha viene de la vista, del oído o de que otro guardia dio
            // la alarma, y no le hace falta saberlo. Eso es la separación de
            // capas funcionando.
            NivelSospecha nivel = Pizarra.Get(ClavesBB.Nivel, NivelSospecha.Tranquilo);

            if (nivel == NivelSospecha.Alerta)
            {
                // TODO · descomentad cuando tengáis EstadoPerseguir
                // StateMachine.ChangeState(new EstadoPerseguir(StateMachine));
                return;
            }

            if (nivel == NivelSospecha.Sospecha)
            {
                // TODO · descomentad cuando tengáis EstadoInvestigar
                // StateMachine.ChangeState(new EstadoInvestigar(StateMachine));
                return;
            }

            // ── 2. Si no, seguir la ruta ────────────────────────────────────
            if (Pizarra.Get(ClavesBB.HaLlegado, false))
            {
                RutaPatrulla ruta = Agente.GetComponent<RutaPatrulla>();
                if (ruta != null && ruta.TieneRuta)
                {
                    Pizarra.Set(ClavesBB.Destino, ruta.Avanzar());
                    Pizarra.Set(ClavesBB.TieneDestino, true);
                }
            }
        }

        internal override void Exit()
        {
            // Aquí no hay nada que limpiar. Pero el método existe y está vacío
            // a propósito: cuando en Enter() arranquéis una animación, un bark
            // o una corrutina, Exit() es donde se apaga. Si no lo hacéis, os
            // quedaréis con guardias diciendo «¿quién anda ahí?» mientras
            // persiguen. Pasa todos los años.
        }
    }
}
