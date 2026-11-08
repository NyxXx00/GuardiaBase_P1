// ═══════════════════════════════════════════════════════════════════════════
//  DecisorFSM.cs — Cerebro versión 1: máquina de estados
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  APARTADO B DEL ENUNCIADO.
//
//  Aquí solo se monta la máquina y se arranca. El comportamiento vive en los
//  estados, en Scripts/FSM/Estados/. Os doy EstadoPatrullar entero como
//  ejemplo del patrón; los otros tres son vuestros.
// ═══════════════════════════════════════════════════════════════════════════

using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    public class DecisorFSM : IDecisor
    {
        private readonly GuardiaAgente _agente;
        private StateMachine _maquina;

        public DecisorFSM(GuardiaAgente agente) { _agente = agente; }

        public void Iniciar(Blackboard bb)
        {
            _maquina = new StateMachine(_agente, bb);
            _maquina.Start(new EstadoPatrullar(_maquina));
        }

        public void Decidir(Blackboard bb)
        {
            _maquina.Execute();
        }

        public string Traza
        {
            get { return "FSM: " + (_maquina == null ? "-" : _maquina.CurrentName); }
        }
    }
}
