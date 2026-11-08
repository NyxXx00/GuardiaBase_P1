// ═══════════════════════════════════════════════════════════════════════════
//  GuardiaAgente.cs — El ciclo Percibir → Pensar → Actuar
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  MATERIAL DE PARTIDA — no hace falta que lo modifiquéis.
//
//  Este componente es el que sostiene la arquitectura de capas del §4 del
//  enunciado. Fijaos en que son literalmente tres líneas en Update(): esa es
//  toda la magia. Lo demás es respetar quién puede llamar a quién.
// ═══════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    /// <summary>Qué cerebro usa este guardia. Se cambia en el inspector, en caliente.</summary>
    public enum TipoDecisor
    {
        MaquinaDeEstados,
        ArbolDeComportamiento
    }

    [RequireComponent(typeof(LocomocionNavMesh))]
    public class GuardiaAgente : MonoBehaviour
    {
        [Header("Cerebro")]
        [Tooltip("El apartado D del enunciado: cambiar esto NO puede exigir " +
                 "tocar la percepción ni la locomoción.")]
        public TipoDecisor Cerebro = TipoDecisor.MaquinaDeEstados;

        [Header("LOD de IA (sesión 3)")]
        [Tooltip("Ciclos de decisión por segundo. La percepción y el movimiento " +
                 "siguen yendo a frame completo; lo que se abarata es PENSAR. " +
                 "Con 10 Hz no se nota y cuesta seis veces menos que a 60.")]
        [Range(1f, 60f)]
        public float FrecuenciaDecision = 10f;

        [Header("Depuración")]
        public bool MostrarTraza = false;

        /// <summary>La pizarra de este guardia. Es el ÚNICO punto de contacto entre capas.</summary>
        public Blackboard Pizarra { get; private set; }

        /// <summary>Lo que decidió en el último ciclo. Lo pinta GuardiaGizmos.</summary>
        public string UltimaTraza { get; private set; } = "-";

        private readonly List<ISensor> _sensores = new List<ISensor>();
        private IDecisor _decisor;
        private LocomocionNavMesh _locomocion;

        private float _proximaDecision;
        private TipoDecisor _cerebroMontado;

        private void Awake()
        {
            Pizarra = new Blackboard();

            // Valores iniciales: una pizarra vacía es una fuente inagotable de
            // NullReferenceException. Sembradla SIEMPRE.
            Pizarra.Set(ClavesBB.Visible, false);
            Pizarra.Set(ClavesBB.Sospecha, 0f);
            Pizarra.Set(ClavesBB.Nivel, NivelSospecha.Tranquilo);
            Pizarra.Set(ClavesBB.TieneUltimaPos, false);
            Pizarra.Set(ClavesBB.UltimaPos, Vector3.zero);
            Pizarra.Set(ClavesBB.VecesPerdido, 0);
            Pizarra.Set(ClavesBB.TieneDestino, false);
            Pizarra.Set(ClavesBB.Destino, transform.position);
            Pizarra.Set(ClavesBB.HaLlegado, true);
            Pizarra.Set(ClavesBB.AlertaDeGrupo, false);

            // Todos los sensores que lleve el GameObject, sean los que sean.
            // Añadir oído en el apartado opcional es soltar un componente aquí.
            GetComponents(_sensores);

            _locomocion = GetComponent<LocomocionNavMesh>();

            MontarCerebro(Cerebro);
        }

        private void MontarCerebro(TipoDecisor tipo)
        {
            _decisor = tipo == TipoDecisor.MaquinaDeEstados
                ? (IDecisor)new DecisorFSM(this)
                : new DecisorBT(this);

            _decisor.Iniciar(Pizarra);
            _cerebroMontado = tipo;
        }

        private void Update()
        {
            // El tiempo lo lleva la pizarra porque el runtime del árbol NO
            // depende de UnityEngine y no puede preguntarle a Time.time.
            Pizarra.Tiempo = Time.time;

            // Permitir cambiar de cerebro en caliente, en pleno Play. Es la
            // forma más rápida de comprobar que las dos versiones se comportan
            // igual, y la que quiero ver en el vídeo de la entrega.
            if (Cerebro != _cerebroMontado) MontarCerebro(Cerebro);

            // ── 1. PERCIBIR ── los sensores ESCRIBEN en la pizarra
            for (int i = 0; i < _sensores.Count; i++)
                _sensores[i].Percibir(Pizarra);

            // ── 2. PENSAR ── el decisor LEE la pizarra y escribe intenciones
            if (Time.time >= _proximaDecision)
            {
                _proximaDecision = Time.time + 1f / Mathf.Max(1f, FrecuenciaDecision);

                Trazador.Activo = MostrarTraza;
                _decisor.Decidir(Pizarra);
                if (MostrarTraza) UltimaTraza = _decisor.Traza;
            }

            // ── 3. ACTUAR ── la locomoción LEE el destino y mueve
            _locomocion.Actuar(Pizarra);
        }
    }
}
