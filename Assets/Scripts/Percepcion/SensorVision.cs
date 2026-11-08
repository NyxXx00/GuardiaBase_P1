// ═══════════════════════════════════════════════════════════════════════════
//  SensorVision.cs — LA PIEZA PRINCIPAL DE LA PRÁCTICA
//
//  Inteligencia Artificial (IA3) · GDDVV 3º · UDIT · Curso 2026/2027
//  Dr. Luis Peña Sánchez
//
//  ⚠️  LO QUE OS DOY AQUÍ ESTÁ DELIBERADAMENTE MAL.
//
//  Tal y como está, este sensor detecta por DISTANCIA Y NADA MÁS. Es decir,
//  es exactamente el primero de los errores típicos de la sesión 4: el guardia
//  os ve a través de las paredes y de espaldas.
//
//  Está así a propósito. Arrancad el proyecto, jugad treinta segundos y veréis
//  el problema con vuestros propios ojos. Vuestro trabajo en el apartado A del
//  enunciado es convertir esto en un sensor que se pueda defender:
//
//     ETAPA 1  alcance   ✅ hecho (es lo único que hay)
//     ETAPA 2  ángulo    ❌ TODO
//     ETAPA 3  oclusión  ❌ TODO   ← y con VARIOS puntos, no solo el centro
//     MEDIDOR  sospecha  ❌ TODO   ← 3 factores, decaimiento, histéresis
//
//  Filtrar barato antes de filtrar caro (sesión 4): el orden de las tres
//  etapas no es casual. La comprobación de distancia es un producto escalar;
//  el raycast es una consulta al motor de físicas. Poner el raycast primero
//  «funciona» y cuesta veinte veces más.
// ═══════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UDIT.IA3.BT;

namespace UDIT.IA3.Guardia
{
    public class SensorVision : MonoBehaviour, ISensor
    {
        [Header("Objetivo")]
        [Tooltip("El jugador. Si lo dejáis vacío se busca por tag al arrancar.")]
        public Transform Objetivo;
        public string TagObjetivo = "Player";

        [Header("Cono de visión")]
        public float Alcance = 15f;
        [Range(0f, 180f)] public float AnguloVision = 60f;

        [Tooltip("Desde dónde se mira. Si está vacío se usa el propio transform. " +
                 "Poned aquí la cabeza: mirar desde los pies da resultados raros.")]
        public Transform Ojos;

        [Tooltip("Qué capas TAPAN la visión. Marcad aquí las paredes. " +
                 "Si dejáis esto en Nothing, no habrá oclusión y se notará.")]
        public LayerMask CapasQueOcluyen;

        [Header("Medidor de sospecha — APARTADO A DEL ENUNCIADO")]
        [Tooltip("Cuánto sube por segundo con evidencia máxima.")]
        public float VelocidadSubida = 0.8f;

        [Tooltip("Cuánto baja por segundo sin ver nada. Debe ser MUY inferior " +
                 "a la de subida: subir en 1-2 s, olvidar en 10-15 s.")]
        public float VelocidadBajada = 0.08f;

        [Tooltip("Umbral tranquilo → sospecha (al subir).")]
        [Range(0f, 1f)] public float UmbralSospecha = 0.35f;

        [Tooltip("Umbral sospecha → alerta (al subir).")]
        [Range(0f, 1f)] public float UmbralAlerta = 0.8f;

        [Tooltip("Cuánto hay que bajar por debajo del umbral para volver atrás. " +
                 "Sin esto el guardia parpadea entre estados. Es la HISTÉRESIS.")]
        [Range(0f, 0.5f)] public float Histeresis = 0.1f;

        // Estado interno del sensor. Lo publica en la pizarra al final de Percibir.
        private float _sospecha;
        private NivelSospecha _nivel = NivelSospecha.Tranquilo;
        private bool _visiblePrevio;

        /// <summary>Solo para los gizmos: si la última comprobación dio positivo.</summary>
        public bool VisibleAhora { get; private set; }
        public float SospechaActual { get { return _sospecha; } }
        public NivelSospecha NivelActual { get { return _nivel; } }

        private void Awake()
        {
            if (Ojos == null) Ojos = transform;

            if (Objetivo == null && !string.IsNullOrEmpty(TagObjetivo))
            {
                GameObject go = GameObject.FindGameObjectWithTag(TagObjetivo);
                if (go != null) Objetivo = go.transform;
            }
        }

        // ───────────────────────────────────────────────────────────────────
        //  EL CICLO DE PERCEPCIÓN
        // ───────────────────────────────────────────────────────────────────

        public void Percibir(Blackboard bb)
        {
            if (Objetivo == null) return;

            // ── ¿Lo veo? ────────────────────────────────────────────────────
            bool visible = ComprobarVision();
            VisibleAhora = visible;

            // ── El medidor ──────────────────────────────────────────────────
            ActualizarSospecha(visible, Time.deltaTime);

            // ── Publicar en la pizarra ──────────────────────────────────────
            bb.Set(ClavesBB.Visible, visible);
            bb.Set(ClavesBB.Sospecha, _sospecha);
            bb.Set(ClavesBB.Nivel, _nivel);

            // La última posición conocida se escribe EN EL INSTANTE en que se
            // pierde el contacto. Ni antes (aún lo veo) ni después (ya no sé
            // dónde estaba).
            if (visible)
            {
                bb.Set(ClavesBB.UltimaPos, Objetivo.position);
                bb.Set(ClavesBB.TieneUltimaPos, true);
            }
            else if (_visiblePrevio)
            {
                bb.Set(ClavesBB.UltimaPos, Objetivo.position);
                bb.Set(ClavesBB.TieneUltimaPos, true);
                bb.Set(ClavesBB.VecesPerdido, bb.Get(ClavesBB.VecesPerdido, 0) + 1);
            }

            _visiblePrevio = visible;
        }

        // ───────────────────────────────────────────────────────────────────
        //  LAS TRES ETAPAS
        // ───────────────────────────────────────────────────────────────────

        private bool ComprobarVision()
        {
            // ETAPA 1 · ALCANCE ✅
            // Comparamos distancias al cuadrado para ahorrarnos la raíz.
            Vector3 haciaObjetivo = Objetivo.position - Ojos.position;

            if (haciaObjetivo.sqrMagnitude > Alcance * Alcance) return false;

            // ETAPA 2 · ÁNGULO ❌ TODO
            //
            // Comprobad que el objetivo cae dentro del cono de AnguloVision
            // grados alrededor de Ojos.forward.
            //
            // Pista: Vector3.Angle(Ojos.forward, haciaObjetivo) <= AnguloVision * 0.5f
            // Cuidado con el 0.5: AnguloVision es el cono COMPLETO.
            // Y cuidado con la componente Y si vuestro suelo no es plano.

            // ETAPA 3 · OCLUSIÓN ❌ TODO
            //
            // Lanzad un Physics.Raycast desde Ojos.position hacia VARIOS puntos
            // del objetivo, no solo hacia su centro. Con tres basta: cabeza
            // (+1.6f en Y), torso (+0.9f) y pies (+0.1f).
            //
            // Si CUALQUIERA de los tres llega sin chocar contra CapasQueOcluyen,
            // el objetivo es visible. Devolved además cuántos han llegado: es
            // un factor estupendo para el medidor (ver «visibilidad parcial»).
            //
            // Pista:
            //   Physics.Raycast(Ojos.position, dir.normalized, dist, CapasQueOcluyen)
            //   devuelve TRUE si ha chocado, es decir, si NO se ve.

            return true;   // ← mientras no hagáis las etapas 2 y 3, ve a través de las paredes
        }

        // ───────────────────────────────────────────────────────────────────
        //  EL MEDIDOR DE SOSPECHA
        // ───────────────────────────────────────────────────────────────────

        private void ActualizarSospecha(bool visible, float dt)
        {
            // TODO · APARTADO A.2 y A.3 del enunciado
            //
            // Ahora mismo esto es un INTERRUPTOR: 0 o 1, sin matices y sin
            // memoria. Es justo lo que el enunciado os pide sustituir.
            //
            // Lo que tiene que hacer:
            //
            //  1) Si es visible, calcular la EVIDENCIA en [0,1] combinando al
            //     menos TRES factores:
            //        · distancia  — más cerca, más evidencia
            //        · ángulo     — más centrado en el cono, más evidencia
            //        · uno vuestro — iluminación, si se mueve, si va agachado,
            //                        cuántos raycasts han llegado, tiempo
            //                        continuado en el cono...
            //
            //     Y DECIDID si los combináis por PRODUCTO o por MEDIA. No es lo
            //     mismo (sesión 4, diapositiva 5): con el producto, un solo
            //     factor a cero anula la detección; con la media, no. Es una
            //     decisión de diseño y hay que justificarla en la memoria.
            //
            //  2) Subir:  _sospecha += evidencia * VelocidadSubida * dt
            //     Bajar:  _sospecha -= VelocidadBajada * dt
            //     ...y Mathf.Clamp01 al final. La ASIMETRÍA entre las dos
            //     velocidades ES el comportamiento: sube en 1-2 s, olvida en
            //     10-15 s.
            //
            //  3) Cruzar umbrales CON HISTÉRESIS (método de abajo).

            _sospecha = visible ? 1f : 0f;          // ← interruptor. Sustituidlo.

            _nivel = ActualizarNivel(_sospecha, _nivel);
        }

        /// <summary>
        /// Umbrales con histéresis: para SUBIR de nivel hace falta pasar el
        /// umbral; para BAJAR hace falta caer por debajo del umbral MENOS la
        /// histéresis. Sin esto, un valor oscilando alrededor de 0,35 hace que
        /// el guardia entre y salga de sospecha varias veces por segundo, y en
        /// pantalla parece que le da un ataque.
        ///
        /// Este método OS LO DOY HECHO porque es fácil equivocarse y no es lo
        /// interesante. Lo interesante es el medidor de arriba.
        /// </summary>
        private NivelSospecha ActualizarNivel(float s, NivelSospecha actual)
        {
            switch (actual)
            {
                case NivelSospecha.Tranquilo:
                    if (s >= UmbralSospecha) return NivelSospecha.Sospecha;
                    break;

                case NivelSospecha.Sospecha:
                    if (s >= UmbralAlerta) return NivelSospecha.Alerta;
                    if (s < UmbralSospecha - Histeresis) return NivelSospecha.Tranquilo;
                    break;

                case NivelSospecha.Alerta:
                    if (s < UmbralAlerta - Histeresis) return NivelSospecha.Sospecha;
                    break;
            }
            return actual;
        }
    }
}
