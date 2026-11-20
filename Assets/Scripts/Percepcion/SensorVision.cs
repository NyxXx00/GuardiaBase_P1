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
        public float Alcance = 20f;
        [Range(0f, 180f)] public float AnguloVision = 80f;

        [Tooltip("Desde dónde se mira. Si está vacío se usa el propio transform. " +
                 "Poned aquí la cabeza: mirar desde los pies da resultados raros.")]
        public Transform Ojos;

        [Tooltip("Qué capas TAPAN la visión. Marcad aquí las paredes. " +
                 "Si dejáis esto en Nothing, no habrá oclusión y se notará.")]
        public LayerMask CapasQueOcluyen;

        [Header("Puntos de comprobación")]
        public float AlturaCentro = 0f;
        public float AlturaCabeza = 0.5f;
        public float AlturaPies = -1f;
        [Tooltip("Separación a izquierda y derecha del centro (simula brazos).")]
        public float SeparacionLateral = 0.35f;

        [Header("Visibilidad por puntos")]
        [Tooltip("Cuántos de los 5 raycasts deben llegar para considerar al jugador VISIBLE.")]
        public int RayosMaxVis = 3;
        [Tooltip("Multiplicador de la subida con 2 puntos visibles.")]
        public float MultiDosPuntos = 0.5f;
        [Tooltip("Multiplicador de la subida con 1 punto visible.")]
        public float MultiUnPunto = 0.25f;

        [Header("Factores de la evidencia")]
        [Tooltip("A esta distancia o menos, con el jugador VISIBLE (RayosMaxVis o más rayos y dentro del cono), la sospecha salta a 1 al instante.")]
        public float DistanciaMaxEvidencia = 3f;
        [Tooltip("Factor distancia a máximo alcance.")]
        [Range(0f, 1f)] public float FactorMinimoLejos = 0.25f;
        [Tooltip("Factor ángulo en el borde del cono (en el centro vale 1).")]
        [Range(0f, 1f)] public float FactorMinimoBorde = 0.25f;

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

        [Header("Búsqueda del último punto")]
        [Tooltip("Mientras el guardia no haya llegado al último punto donde vio al jugador " +
                 "(y esté en Sospecha o Alerta), la sospecha NO baja.")]
        public bool CongelarSospechaAlBuscar = true;
        [Tooltip("A esta distancia (en horizontal) del último punto de contacto se considera " +
                 "que el guardia ha llegado y la sospecha vuelve a bajar.")]
        public float DistanciaLlegada = 1.5f;
        [Tooltip("Seguridad: tras estos segundos sin ver al jugador, la sospecha vuelve a bajar " +
                 "aunque el guardia no haya llegado (por si se atasca por el camino).")]
        public float TiempoMaxBusqueda = 20f;

        // Estado interno del sensor. Lo publica en la pizarra al final de Percibir.
        private float _sospecha;
        private NivelSospecha _nivel = NivelSospecha.Tranquilo;
        private bool _visiblePrevio;
        private float _anguloActual;

        // Último punto donde hubo contacto (algún raycast llegó) y cuánto hace.
        private Vector3 _ultimoContacto;
        private bool _hayContacto;
        private float _tSinContacto;

        private const int NumPuntos = 5;
        private readonly Vector3[] _puntos = new Vector3[NumPuntos];
        private readonly bool[] _puntoVisible = new bool[NumPuntos];
        private bool _puntosValidos;

        /// <summary>Solo para los gizmos: si la última comprobación dio positivo.</summary>
        public bool VisibleAhora { get; private set; }
        public int RayosVisibles { get; private set; }
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
            int rayos;
            float distancia;
            ComprobarVision(out rayos, out distancia);

            RayosVisibles = rayos;
            bool visible = rayos >= RayosMaxVis;
            VisibleAhora = visible;

            // ── El medidor ──────────────────────────────────────────────────
            ActualizarSospecha(rayos, distancia, Time.deltaTime);

            // ── Publicar en la pizarra ──────────────────────────────────────
            bb.Set(ClavesBB.Visible, visible);
            bb.Set(ClavesBB.Sospecha, _sospecha);
            bb.Set(ClavesBB.Nivel, _nivel);
            if (rayos > 0) bb.Set(ClavesBB.PosSospecha, Objetivo.position);

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

        private void ComprobarVision(out int rayosVisibles, out float distancia)
        {
            rayosVisibles = 0;
            _puntosValidos = false;
            _anguloActual = 0f;

            // ETAPA 1 · ALCANCE ✅
            // Comparamos distancias al cuadrado para ahorrarnos la raíz.
            // ETAPA 2 · ÁNGULO ❌ TODO
            //
            // Comprobad que el objetivo cae dentro del cono de AnguloVision
            // grados alrededor de Ojos.forward.
            //
            // Pista: Vector3.Angle(Ojos.forward, haciaObjetivo) <= AnguloVision * 0.5f
            // Cuidado con el 0.5: AnguloVision es el cono COMPLETO.
            // Y cuidado con la componente Y si vuestro suelo no es plano.

            Vector3 centro = Objetivo.position + Vector3.up * AlturaCentro;
            Vector3 haciaObjetivo = centro - Ojos.position;
            float sqrDist = haciaObjetivo.sqrMagnitude;
            distancia = Mathf.Sqrt(sqrDist);

            if (sqrDist > Alcance * Alcance) return;

            Vector3 dirAngulo = haciaObjetivo;
            Vector3 frente = Ojos.forward;
            dirAngulo.y = 0f;
            frente.y = 0f;

            if (dirAngulo.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f)
            {
                _anguloActual = Vector3.Angle(frente, dirAngulo);
                if (_anguloActual > AnguloVision * 0.5f) return;
            }

            // ETAPA 3 · OCLUSIÓN ❌ TODO
            //
            // Lanzad un Physics.Raycast desde Ojos.position hacia VARIOS puntos
            // del objetivo, no solo hacia su centro. Con tres basta: cabeza
            // (+1.6f en Y), torso (+0.9f) y pies (+0.1f) si el pivote está en
            // los pies. La cápsula de Unity lo tiene en el CENTRO: con ella son
            // +0.8f, 0f y -0.9f (README_Montaje, 3.3).
            //
            // Si CUALQUIERA de los tres llega sin chocar contra CapasQueOcluyen,
            // el objetivo es visible. Devolved además cuántos han llegado: es
            // un factor estupendo para el medidor (ver «visibilidad parcial»).
            //
            // Pista:
            //   Physics.Raycast(Ojos.position, dir.normalized, dist, CapasQueOcluyen)
            //   devuelve TRUE si ha chocado, es decir, si NO se ve.

            Vector3 lado = Vector3.Cross(Vector3.up, dirAngulo);
            lado = lado.sqrMagnitude > 0.0001f ? lado.normalized : Ojos.right;

            Vector3 basePos = Objetivo.position;
            _puntos[0] = centro;
            _puntos[1] = basePos + Vector3.up * AlturaCabeza;
            _puntos[2] = basePos + Vector3.up * AlturaPies;
            _puntos[3] = centro - lado * SeparacionLateral;
            _puntos[4] = centro + lado * SeparacionLateral;
            _puntosValidos = true;

            for (int i = 0; i < NumPuntos; i++)
            {
                _puntoVisible[i] = PuntoVisible(_puntos[i]);
                if (_puntoVisible[i]) rayosVisibles++;
            }
        }

        private bool PuntoVisible(Vector3 punto)
        {
            Vector3 origen = Ojos.position;
            Vector3 dir = punto - origen;
            float dist = dir.magnitude;
            if (dist < 0.001f) return true;

            return !Physics.Raycast(origen, dir / dist, dist - 0.02f, CapasQueOcluyen,
                                    QueryTriggerInteraction.Ignore);
        }

        // ───────────────────────────────────────────────────────────────────
        //  EL MEDIDOR DE SOSPECHA
        // ───────────────────────────────────────────────────────────────────

        private void ActualizarSospecha(int rayos, float distancia, float dt)
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
            //     mismo (sesión 4, «Producto o suma» de B2.1b): con el producto, un solo
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

            if (rayos > 0)
            {
                // Hay contacto: recordamos dónde está y reiniciamos el contador.
                _ultimoContacto = Objetivo.position;
                _hayContacto = true;
                _tSinContacto = 0f;

                // Visible y dentro del radio de máxima evidencia → salta a 1 al instante.
                if (rayos >= RayosMaxVis && distancia <= DistanciaMaxEvidencia)
                {
                    _sospecha = 1f;
                }
                else
                {
                    float cercania = Mathf.InverseLerp(Alcance, DistanciaMaxEvidencia, distancia);
                    float factorDistancia = Mathf.Lerp(FactorMinimoLejos, 1f, cercania);

                    float factorVisibilidad;
                    if (rayos >= RayosMaxVis) factorVisibilidad = 1f;
                    else if (rayos == 2) factorVisibilidad = MultiDosPuntos;
                    else factorVisibilidad = MultiUnPunto;

                    float t = Mathf.Clamp01(_anguloActual / Mathf.Max(0.01f, AnguloVision * 0.5f));
                    float factorAngulo = Mathf.Lerp(1f, FactorMinimoBorde, t);

                    float evidencia = factorDistancia * factorVisibilidad * factorAngulo;
                    _sospecha += evidencia * VelocidadSubida * dt;
                }
            }
            else
            {
                if (!DebeMantenerSospecha(dt))
                    _sospecha -= VelocidadBajada * dt;
            }

            _sospecha = Mathf.Clamp01(_sospecha);

            _nivel = ActualizarNivel(_sospecha, _nivel);
        }

        /// <summary>
        /// TRUE si la sospecha no debe bajar porque el guardia aún va hacia el último
        /// punto donde vio al jugador. Deja de mantenerla cuando llega (a
        /// DistanciaLlegada), cuando pasa TiempoMaxBusqueda sin ver al jugador, o si
        /// el guardia está Tranquilo (no está investigando nada).
        /// </summary>
        private bool DebeMantenerSospecha(float dt)
        {
            if (!CongelarSospechaAlBuscar || !_hayContacto) return false;

            _tSinContacto += dt;

            Vector3 aPunto = _ultimoContacto - transform.position;
            aPunto.y = 0f;
            bool llego = aPunto.sqrMagnitude <= DistanciaLlegada * DistanciaLlegada;

            if (llego || _tSinContacto > TiempoMaxBusqueda)
            {
                _hayContacto = false;   // búsqueda terminada: vuelve el olvido
                return false;
            }

            return _nivel != NivelSospecha.Tranquilo;
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