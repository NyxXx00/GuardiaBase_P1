// ═══════════════════════════════════════════════════════════════════════════
//  StateMachine.cs — Patrón Estado
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  MATERIAL DE PARTIDA — es EXACTAMENTE el de las transparencias de la
//  sesión 7, con una sola diferencia: allí StateMachine era un MonoBehaviour
//  que se auto-tickeaba con InvokeRepeating("Think", 0, 0.5f). Aquí no lo es,
//  porque quien marca el ritmo es GuardiaAgente (con su FrecuenciaDecision),
//  para que la FSM y el árbol se ticken igual y la comparación sea justa.
//
//  Es la misma idea: pensar a 2-10 Hz, no a 60.
// ═══════════════════════════════════════════════════════════════════════════

using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    /// <summary>
    /// Un estado del guardia. Una clase por estado: eso es el patrón Estado,
    /// y es lo que se os pide en el apartado B del enunciado.
    ///
    /// El switch gigante de la diapositiva 5 también «funciona». La diferencia
    /// es que añadir un estado ahí toca todos los case, y aquí es un fichero
    /// nuevo que no obliga a tocar nada.
    /// </summary>
    public abstract class State
    {
        protected StateMachine StateMachine;

        protected State(StateMachine machine) { StateMachine = machine; }

        /// <summary>La pizarra. Se LEE. Los estados no perciben.</summary>
        protected Blackboard Pizarra { get { return StateMachine.Pizarra; } }

        /// <summary>El agente, por si necesitáis su transform o su ruta.</summary>
        protected GuardiaAgente Agente { get { return StateMachine.Agente; } }

        /// <summary>Una vez, al entrar. Aquí van los preparativos y los barks.</summary>
        internal virtual void Enter() { }

        /// <summary>Una vez por ciclo de decisión. Aquí se decide y se transita.</summary>
        internal virtual void Execute() { }

        /// <summary>Una vez, al salir. Aquí se limpia lo que dejó Enter.</summary>
        internal virtual void Exit() { }
    }

    /// <summary>
    /// La máquina. Guarda el estado actual y gestiona las transiciones.
    ///
    /// Fijaos en lo poco que hace: toda la inteligencia está repartida en los
    /// estados. Ése es justo el argumento a favor y en contra de una FSM.
    /// </summary>
    public class StateMachine
    {
        public GuardiaAgente Agente { get; private set; }
        public Blackboard Pizarra { get; private set; }

        private State _currentState;

        /// <summary>El anterior. Hace falta para «volver a lo que estaba haciendo» (requisito R+).</summary>
        public State PreviousState { get; private set; }

        /// <summary>Nombre del estado actual. Para los gizmos y para la memoria.</summary>
        public string CurrentName
        {
            get { return _currentState == null ? "-" : _currentState.GetType().Name; }
        }

        public StateMachine(GuardiaAgente agente, Blackboard pizarra)
        {
            Agente = agente;
            Pizarra = pizarra;
        }

        /// <summary>
        /// Arranca la máquina en un estado. OJO: llama a Enter().
        ///
        /// Es un fallo clásico montar la máquina asignando el estado inicial
        /// directamente y no llamar nunca a su Enter(): el primer estado se
        /// queda sin inicializar y el bug aparece diez minutos después, en
        /// otro sitio.
        /// </summary>
        public void Start(State inicial)
        {
            _currentState = inicial;
            _currentState.Enter();
        }

        /// <summary>Un ciclo de decisión.</summary>
        public void Execute()
        {
            if (_currentState != null) _currentState.Execute();
        }

        public void ChangeState(State newState)
        {
            if (newState == null || newState == _currentState) return;

            if (_currentState != null) _currentState.Exit();

            PreviousState = _currentState;
            _currentState = newState;
            _currentState.Enter();
        }
    }
}
