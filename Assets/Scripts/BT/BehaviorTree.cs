// ═══════════════════════════════════════════════════════════════════════════
//  BehaviorTree.cs — Runtime mínimo de Árbol de Comportamiento
//
//  Inteligencia Artificial · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  Este fichero es EL MATERIAL QUE OS DOY para la Práctica 1. Contiene los
//  cimientos del árbol: el contrato de nodo, los composites, los decoradores
//  y el blackboard. Lo que escribís vosotros son los NODOS HOJA de vuestro
//  dominio y el ÁRBOL que los combina (ver GuardBehaviour.cs).
//
//  Deliberadamente NO depende de UnityEngine: así se puede probar en una
//  consola, y así queda claro que un árbol de comportamiento no es una cosa
//  «de Unity», sino un patrón de diseño.
//
//  Basado en: Champandard, A. J. y Dunstan, P., "The Behavior Tree Starter
//  Kit", Game AI Pro, cap. 6 (el original está en C++).
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Text;

namespace UDIT.IA3.BT
{
    // ───────────────────────────────────────────────────────────────────────
    //  ESTADOS DE RETORNO
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Resultado de ejecutar un nodo durante un tick.</summary>
    public enum Status
    {
        /// <summary>Aún no se ha ejecutado nunca.</summary>
        Invalid,
        /// <summary>La tarea terminó bien.</summary>
        Success,
        /// <summary>La tarea no se pudo completar.</summary>
        Failure,
        /// <summary>Sigue en curso: dame otro tick.</summary>
        Running,
        /// <summary>La abortaron desde fuera antes de terminar.</summary>
        Aborted
    }

    // ───────────────────────────────────────────────────────────────────────
    //  BLACKBOARD
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La memoria compartida del agente. El árbol NO percibe: lee de aquí.
    /// La percepción y la locomoción escriben aquí. Esa separación es lo que
    /// permite cambiar el árbol sin tocar nada más.
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> _datos =
            new Dictionary<string, object>();

        /// <summary>Tiempo de juego en segundos. Lo actualiza el host.</summary>
        public float Tiempo { get; set; }

        public void Set<T>(string clave, T valor) { _datos[clave] = valor; }

        public T Get<T>(string clave, T porDefecto = default(T))
        {
            object v;
            if (_datos.TryGetValue(clave, out v) && v is T) return (T)v;
            return porDefecto;
        }

        public bool Has(string clave) { return _datos.ContainsKey(clave); }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var kv in _datos) sb.Append(kv.Key).Append('=')
                                        .Append(kv.Value).Append("  ");
            return sb.ToString();
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  NODO
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Base de todos los nodos del árbol.
    ///
    /// EL CONTRATO, que es lo importante de esta clase:
    ///   · OnInitialize()  se llama UNA vez, justo antes del primer Update()
    ///   · Update()        se llama UNA vez por tick, mientras devuelva Running
    ///   · OnTerminate(s)  se llama UNA vez, en cuanto Update() deja de devolver
    ///                     Running
    ///
    /// Tick() es quien garantiza ese contrato, y por eso NO es virtual: si
    /// cada nodo tuviera que acordarse de llamar a su propio Enter/Exit,
    /// tarde o temprano alguno se olvidaría. Es el error que cometíamos a mano
    /// en la máquina de estados.
    /// </summary>
    public abstract class Node
    {
        public string Nombre;

        /// <summary>Último estado devuelto. Sirve para depurar y para el contrato.</summary>
        public Status Estado { get; protected set; }

        protected Node(string nombre = null)
        {
            Nombre = nombre ?? GetType().Name;
            Estado = Status.Invalid;
        }

        /// <summary>Ejecuta el nodo durante este tick. No se sobreescribe.</summary>
        public Status Tick(Blackboard bb)
        {
            if (Estado != Status.Running) OnInitialize(bb);

            Estado = Update(bb);

            if (Estado != Status.Running) OnTerminate(bb, Estado);

            Trazador.Registrar(this, Estado);
            return Estado;
        }

        /// <summary>Corta el nodo desde fuera (lo usa el selector activo).</summary>
        public virtual void Abort(Blackboard bb)
        {
            if (Estado == Status.Running)
            {
                OnTerminate(bb, Status.Aborted);
                Estado = Status.Aborted;
            }
        }

        /// <summary>Vuelve a dejar el nodo como recién creado.</summary>
        public virtual void Reset() { Estado = Status.Invalid; }

        protected virtual void OnInitialize(Blackboard bb) { }

        protected abstract Status Update(Blackboard bb);

        protected virtual void OnTerminate(Blackboard bb, Status resultado) { }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  TRAZADOR  (para ver el árbol por dentro mientras se ejecuta)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registra qué nodos se han ejecutado en el tick actual. Un árbol sin
    /// traza es imposible de depurar: no ves por qué rama ha bajado.
    /// </summary>
    public static class Trazador
    {
        public static bool Activo = false;
        public static readonly List<string> Tick = new List<string>();

        internal static void Registrar(Node n, Status s)
        {
            if (Activo) Tick.Add(n.Nombre + ":" + s);
        }

        public static void Limpiar() { Tick.Clear(); }

        public static string Ultima() { return string.Join(" > ", Tick.ToArray()); }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  COMPOSITES
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Nodo con varios hijos.</summary>
    public abstract class Composite : Node
    {
        protected readonly List<Node> Hijos = new List<Node>();

        protected Composite(string nombre, params Node[] hijos) : base(nombre)
        {
            Hijos.AddRange(hijos);
        }

        public Composite Add(Node hijo) { Hijos.Add(hijo); return this; }

        public override void Reset()
        {
            base.Reset();
            foreach (var h in Hijos) h.Reset();
        }
    }

    /// <summary>
    /// SECUENCIA = Y lógico. Ejecuta los hijos en orden y para en cuanto uno
    /// no tiene éxito. Es como un diseñador escribe un plan.
    /// </summary>
    public class Sequence : Composite
    {
        protected int Actual;

        public Sequence(string nombre, params Node[] hijos)
            : base(nombre, hijos) { }

        protected override void OnInitialize(Blackboard bb) { Actual = 0; }

        protected override Status Update(Blackboard bb)
        {
            if (Hijos.Count == 0) return Status.Success;

            // El bucle es importante: cuando un hijo tiene éxito, el siguiente
            // se procesa EN ESTE MISMO tick. Si no, un árbol profundo tardaría
            // un frame por nivel en llegar a una acción de verdad.
            while (true)
            {
                Status s = Hijos[Actual].Tick(bb);

                if (s != Status.Success) return s;      // Failure o Running

                if (++Actual == Hijos.Count) return Status.Success;
            }
        }

        public override void Abort(Blackboard bb)
        {
            if (Estado == Status.Running && Actual < Hijos.Count)
                Hijos[Actual].Abort(bb);
            base.Abort(bb);
        }
    }

    /// <summary>
    /// SELECTOR = O lógico. El mismo código que la secuencia con Success y
    /// Failure intercambiados: se queda con el primer hijo que funcione.
    /// </summary>
    public class Selector : Composite
    {
        protected int Actual;

        public Selector(string nombre, params Node[] hijos)
            : base(nombre, hijos) { }

        protected override void OnInitialize(Blackboard bb) { Actual = 0; }

        protected override Status Update(Blackboard bb)
        {
            if (Hijos.Count == 0) return Status.Failure;

            while (true)
            {
                Status s = Hijos[Actual].Tick(bb);

                if (s != Status.Failure) return s;      // Success o Running

                if (++Actual == Hijos.Count) return Status.Failure;
            }
        }

        public override void Abort(Blackboard bb)
        {
            if (Estado == Status.Running && Actual < Hijos.Count)
                Hijos[Actual].Abort(bb);
            base.Abort(bb);
        }
    }

    /// <summary>
    /// SELECTOR ACTIVO. Un selector normal se queda pegado al hijo que devolvió
    /// Running y no vuelve a mirar si una rama de MAYOR prioridad se ha vuelto
    /// posible. Éste reevalúa desde la izquierda en cada tick y, si cambia de
    /// rama, aborta la anterior.
    ///
    /// Es lo que hace que ver al jugador interrumpa la patrulla.
    /// </summary>
    public class ActiveSelector : Selector
    {
        public ActiveSelector(string nombre, params Node[] hijos)
            : base(nombre, hijos) { }

        protected override Status Update(Blackboard bb)
        {
            int anterior = Actual;

            Actual = 0;                        // vuelve a empezar por la izquierda
            Status s = base.Update(bb);

            if (anterior != Actual && anterior < Hijos.Count)
                Hijos[anterior].Abort(bb);     // mata la rama que estaba corriendo

            return s;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  PARALELO Y MONITOR
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Política de un nodo paralelo: ¿basta uno o hacen falta todos?</summary>
    public enum Politica { RequireOne, RequireAll }

    /// <summary>
    /// PARALELO. Tickea todos sus hijos en cada Update. OJO: es concurrencia
    /// LÓGICA, no multithreading — los hijos se ejecutan uno detrás de otro
    /// dentro del mismo frame.
    ///
    /// Regla de diseño: el FALLO TIENE PRIORIDAD sobre el éxito. Si en el mismo
    /// tick un hijo tiene éxito y otro falla, el paralelo falla. El árbol asume
    /// siempre el peor caso.
    /// </summary>
    public class Parallel : Composite
    {
        private readonly Politica _exito;
        private readonly Politica _fallo;

        public Parallel(string nombre, Politica exito, Politica fallo,
                        params Node[] hijos)
            : base(nombre, hijos)
        {
            _exito = exito;
            _fallo = fallo;
        }

        protected override Status Update(Blackboard bb)
        {
            int exitos = 0, fallos = 0;

            foreach (var h in Hijos)
            {
                // Los que ya terminaron no se vuelven a ejecutar en esta pasada
                if (h.Estado != Status.Success && h.Estado != Status.Failure)
                    h.Tick(bb);

                if (h.Estado == Status.Success)
                {
                    exitos++;
                    if (_exito == Politica.RequireOne) return Terminar(bb, Status.Success);
                }
                else if (h.Estado == Status.Failure)
                {
                    fallos++;
                    if (_fallo == Politica.RequireOne) return Terminar(bb, Status.Failure);
                }
            }

            // El fallo se comprueba antes que el éxito, a propósito
            if (_fallo == Politica.RequireAll && fallos == Hijos.Count)
                return Terminar(bb, Status.Failure);

            if (_exito == Politica.RequireAll && exitos == Hijos.Count)
                return Terminar(bb, Status.Success);

            return Status.Running;
        }

        /// <summary>Al terminar hay que abortar a los hijos que sigan vivos.</summary>
        private Status Terminar(Blackboard bb, Status resultado)
        {
            foreach (var h in Hijos)
                if (h.Estado == Status.Running) h.Abort(bb);
            return resultado;
        }

        protected override void OnInitialize(Blackboard bb)
        {
            foreach (var h in Hijos) h.Reset();
        }
    }

    /// <summary>
    /// MONITOR. Un paralelo con dos papeles bien separados:
    ///
    ///   · la GUARDA solo LEE el mundo, y se comprueba en CADA tick
    ///   · el CUERPO es el que actúa
    ///
    /// En cuanto la guarda deja de cumplirse, el cuerpo se aborta y el monitor
    /// falla. Como solo un subárbol modifica el mundo, no hay problemas de
    /// sincronización ni de contención.
    ///
    /// POR QUÉ HACE FALTA: una secuencia con la condición delante NO vuelve a
    /// comprobarla. Cuando la secuencia está Running, retoma en el hijo por el
    /// que iba —la acción—, y la condición se queda atrás. El resultado es un
    /// guardia que persigue a un jugador al que ya no ve. Probadlo: cambiad el
    /// Monitor de GuardBehaviour por una Sequence y mirad la traza.
    /// </summary>
    public class Monitor : Node
    {
        private readonly Node _guarda;
        private readonly Node _cuerpo;

        public Monitor(string nombre, Node guarda, Node cuerpo) : base(nombre)
        {
            _guarda = guarda;
            _cuerpo = cuerpo;
        }

        protected override void OnInitialize(Blackboard bb)
        {
            _guarda.Reset();
            _cuerpo.Reset();
        }

        protected override Status Update(Blackboard bb)
        {
            // La guarda se reinicia siempre: queremos el valor de AHORA
            _guarda.Reset();

            if (_guarda.Tick(bb) != Status.Success)
            {
                _cuerpo.Abort(bb);
                return Status.Failure;
            }

            return _cuerpo.Tick(bb);
        }

        public override void Abort(Blackboard bb)
        {
            _cuerpo.Abort(bb);
            base.Abort(bb);
        }

        public override void Reset()
        {
            base.Reset();
            _guarda.Reset();
            _cuerpo.Reset();
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  DECORADORES
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Nodo con exactamente un hijo, cuyo resultado modifica.</summary>
    public abstract class Decorator : Node
    {
        protected readonly Node Hijo;

        protected Decorator(string nombre, Node hijo) : base(nombre)
        {
            Hijo = hijo;
        }

        public override void Reset() { base.Reset(); Hijo.Reset(); }

        public override void Abort(Blackboard bb) { Hijo.Abort(bb); base.Abort(bb); }
    }

    /// <summary>Invierte el resultado del hijo. Running pasa tal cual.</summary>
    public class Inverter : Decorator
    {
        public Inverter(Node hijo) : base("Inverter", hijo) { }

        protected override Status Update(Blackboard bb)
        {
            Status s = Hijo.Tick(bb);
            if (s == Status.Success) return Status.Failure;
            if (s == Status.Failure) return Status.Success;
            return s;
        }
    }

    /// <summary>Devuelve siempre Success. Útil para pasos opcionales.</summary>
    public class Succeeder : Decorator
    {
        public Succeeder(Node hijo) : base("Succeeder", hijo) { }

        protected override Status Update(Blackboard bb)
        {
            Status s = Hijo.Tick(bb);
            return s == Status.Running ? s : Status.Success;
        }
    }

    /// <summary>Repite al hijo n veces mientras tenga éxito.</summary>
    public class Repeat : Decorator
    {
        private readonly int _veces;
        private int _hechas;

        public Repeat(int veces, Node hijo) : base("Repeat x" + veces, hijo)
        {
            _veces = veces;
        }

        protected override void OnInitialize(Blackboard bb) { _hechas = 0; }

        protected override Status Update(Blackboard bb)
        {
            while (true)
            {
                Status s = Hijo.Tick(bb);

                if (s == Status.Running) return Status.Running;
                if (s == Status.Failure) return Status.Failure;

                if (++_hechas >= _veces) return Status.Success;

                Hijo.Reset();   // para que el siguiente Tick vuelva a inicializar
            }
        }
    }

    /// <summary>
    /// Falla si no han pasado `segundos` desde la última vez que el hijo tuvo
    /// éxito. Es el decorador que evita que el guardia grite «¡Alto!» sesenta
    /// veces por segundo.
    /// </summary>
    public class Cooldown : Decorator
    {
        private readonly float _segundos;
        private float _proximoPermitido;

        public Cooldown(float segundos, Node hijo)
            : base("Cooldown " + segundos + "s", hijo)
        {
            _segundos = segundos;
            _proximoPermitido = float.MinValue;
        }

        protected override Status Update(Blackboard bb)
        {
            if (bb.Tiempo < _proximoPermitido) return Status.Failure;

            Status s = Hijo.Tick(bb);

            if (s == Status.Success) _proximoPermitido = bb.Tiempo + _segundos;

            return s;
        }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  HOJAS GENÉRICAS
    //
    //  Con estas dos se puede montar un árbol entero sin escribir una clase
    //  por nodo. Para la práctica os pediré clases propias con nombre, porque
    //  se leen mucho mejor, pero para prototipar van muy bien.
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Condición: lee el mundo y responde sí o no.</summary>
    public class Condition : Node
    {
        private readonly Func<Blackboard, bool> _prueba;

        public Condition(string nombre, Func<Blackboard, bool> prueba)
            : base(nombre)
        {
            _prueba = prueba;
        }

        protected override Status Update(Blackboard bb)
        {
            return _prueba(bb) ? Status.Success : Status.Failure;
        }
    }

    /// <summary>Acción: cambia el mundo. Puede durar varios ticks.</summary>
    public class ActionNode : Node
    {
        private readonly Func<Blackboard, Status> _accion;

        public ActionNode(string nombre, Func<Blackboard, Status> accion)
            : base(nombre)
        {
            _accion = accion;
        }

        protected override Status Update(Blackboard bb) { return _accion(bb); }
    }

    // ───────────────────────────────────────────────────────────────────────
    //  EL ÁRBOL
    // ───────────────────────────────────────────────────────────────────────

    public class BehaviorTree
    {
        public readonly Node Raiz;
        public readonly Blackboard Pizarra;

        public BehaviorTree(Node raiz, Blackboard pizarra = null)
        {
            Raiz = raiz;
            Pizarra = pizarra ?? new Blackboard();
        }

        /// <summary>Un ciclo de pensamiento. No hace falta llamarlo cada frame.</summary>
        public Status Tick()
        {
            Trazador.Limpiar();
            return Raiz.Tick(Pizarra);
        }
    }
}
