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

        [Header("Puntos de comprobación")]
        [Tooltip("Altura del 'centro' del cuerpo sobre el pivote del objetivo.")]
        public float AlturaCentro = 0.9f;
        [Tooltip("Altura de la cabeza sobre el pivote del objetivo.")]
        public float AlturaCabeza = 1.6f;
        [Tooltip("Altura de los pies sobre el pivote del objetivo.")]
        public float AlturaPies = 0.1f;
        [Tooltip("Cuánto se separan a izquierda y derecha la simulación de brazos.")]
        public float SeparacionLateral = 0.35f;

        [Header("Visibilidad por puntos")]
        [Tooltip("Cuántos de los 5 raycasts deben llegar para considerar al jugador VISIBLE.")]
        public int RayosMaxVis = 3;

        [Tooltip("Multiplicador de la subida con 2 puntos visibles (la mitad).")]
        public float MultiDosPuntos = 0.5f;

        [Tooltip("Multiplicador de la subida con 1 punto visible (un cuarto).")]
        public float MultiUnPunto = 0.25f;

        [Tooltip("Por debajo de esta distancia, con el jugador visible, la sospecha salta a 1 al instante.")]
        public float DistanciaInstantanea = 3f;

        [Tooltip("Fracción de la velocidad de subida que se mantiene a distancia máxima (Alcance). " +
                 "0 = a máximo alcance nunca sube; 1 = la distancia no influye.")]
        public float FactorMinimoLejos = 0.2f;

        [Header("Medidor de sospecha — APARTADO A DEL ENUNCIADO")]
        [Tooltip("Cuánto sube por segundo con evidencia máxima.")]
        public float VelocidadSubida = 1.2f;

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

        [Header("Cosita visual")]
        public bool DepurarEnJuego = true;
        public float GrosorLinea = 0.03f;

        // Estado interno del sensor. Lo publica en la pizarra al final de Percibir.
        private float _sospecha;
        private NivelSospecha _nivel = NivelSospecha.Tranquilo;
        private bool _visiblePrevio;

        // Datos de la última comprobación (para gizmos y depuración).
        private const int NumPuntos = 5;
        private readonly Vector3[] _puntos = new Vector3[NumPuntos];
        private readonly bool[] _puntoVisible = new bool[NumPuntos];
        private bool _puntosValidos;

        // Depuración en juego.
        private LineRenderer[] _lineas;
        private GUIStyle _estiloTexto;

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

            if (DepurarEnJuego) CrearLineasDebug();
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

            // ── Depuración en juego ─────────────────────────────────────────
            if (DepurarEnJuego) ActualizarLineasDebug();

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

        private void ComprobarVision(out int rayosVisibles, out float distancia)
        {
            rayosVisibles = 0;
            _puntosValidos = false;

            Vector3 centro = Objetivo.position + Vector3.up * AlturaCentro;

            // ETAPA 1 · ALCANCE ✅
            // Comparamos distancias al cuadrado para ahorrarnos la raíz.
            Vector3 haciaObjetivo = centro - Ojos.position;
            float sqrDist = haciaObjetivo.sqrMagnitude;
            distancia = Mathf.Sqrt(sqrDist);

            if (sqrDist > Alcance * Alcance) return;

            // ETAPA 2 · ÁNGULO ❌ TODO
            //
            // Comprobad que el objetivo cae dentro del cono de AnguloVision
            // grados alrededor de Ojos.forward.
            //
            // Pista: Vector3.Angle(Ojos.forward, haciaObjetivo) <= AnguloVision * 0.5f
            // Cuidado con el 0.5: AnguloVision es el cono COMPLETO.
            // Y cuidado con la componente Y si vuestro suelo no es plano.

            Vector3 dirAngulo = haciaObjetivo;
            Vector3 frente = Ojos.forward;
            dirAngulo.y = 0f;
            frente.y = 0f;

            // Si el objetivo está prácticamente encima, el ángulo no está definido:
            // lo damos por dentro del cono y que decidan los raycasts.
            if (dirAngulo.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f)
            {
                if (Vector3.Angle(frente, dirAngulo) > AnguloVision * 0.5f) return;
            }

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

            // Los laterales son perpendiculares a la dirección guardia → objetivo
            // en el plano horizontal.
            Vector3 lado = Vector3.Cross(Vector3.up, dirAngulo);
            lado = lado.sqrMagnitude > 0.0001f ? lado.normalized : Ojos.right;

            Vector3 basePos = Objetivo.position;
            _puntos[0] = centro;                                // centro
            _puntos[1] = basePos + Vector3.up * AlturaCabeza;   // cabeza
            _puntos[2] = basePos + Vector3.up * AlturaPies;     // pies
            _puntos[3] = centro - lado * SeparacionLateral;     // izquierda
            _puntos[4] = centro + lado * SeparacionLateral;     // derecha
            _puntosValidos = true;

            for (int i = 0; i < NumPuntos; i++)
            {
                _puntoVisible[i] = PuntoVisible(_puntos[i]);
                if (_puntoVisible[i]) rayosVisibles++;
            }
        }

        /// <summary>
        /// TRUE si el punto es visible desde los ojos, es decir, si el raycast
        /// NO choca contra ninguna capa que ocluya.
        /// </summary>
        private bool PuntoVisible(Vector3 punto)
        {
            Vector3 origen = Ojos.position;
            Vector3 dir = punto - origen;
            float dist = dir.magnitude;
            if (dist < 0.001f) return true;

            // Un pelín menos de distancia para no chocar con el propio punto
            // si este queda pegado a una superficie.
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

            if (rayos > 0)
            {
                // Visibilidad completa y muy cerca → instantáneo.
                if (rayos >= RayosMaxVis && distancia <= DistanciaInstantanea)
                {
                    _sospecha = 1f;
                }
                else
                {
                    // Factor distancia: 1 a DistanciaInstantanea o menos,
                    // FactorMinimoLejos a Alcance.
                    float cercania = Mathf.InverseLerp(Alcance, DistanciaInstantanea, distancia);
                    float factorDistancia = Mathf.Lerp(FactorMinimoLejos, 1f, cercania);

                    // Factor visibilidad: 3+ puntos = 1, 2 = mitad, 1 = cuarto.
                    float factorVisibilidad;
                    if (rayos >= RayosMaxVis) factorVisibilidad = 1f;
                    else if (rayos == 2) factorVisibilidad = MultiDosPuntos;
                    else factorVisibilidad = MultiUnPunto;

                    // Producto de factores.
                    float evidencia = factorDistancia * factorVisibilidad;
                    _sospecha += evidencia * VelocidadSubida * dt;
                }
            }
            else
            {
                // Sin ver ningún punto: se olvida lentamente.
                _sospecha -= VelocidadBajada * dt;
            }

            _sospecha = Mathf.Clamp01(_sospecha);

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

        // ───────────────────────────────────────────────────────────────────
        //  DEPURACIÓN EN JUEGO (vista Game)
        // ───────────────────────────────────────────────────────────────────

        /// <summary>Crea un LineRenderer por cada punto, hijo de este objeto.</summary>
        private void CrearLineasDebug()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            Material mat = new Material(shader);
            _lineas = new LineRenderer[NumPuntos];

            for (int i = 0; i < NumPuntos; i++)
            {
                GameObject go = new GameObject("DebugRayo_" + i);
                go.transform.SetParent(transform, false);

                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.startWidth = GrosorLinea;
                lr.endWidth = GrosorLinea;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;

                _lineas[i] = lr;
            }
        }


        private void ActualizarLineasDebug()
        {
            if (_lineas == null) return;

            for (int i = 0; i < NumPuntos; i++)
            {
                LineRenderer lr = _lineas[i];
                if (!_puntosValidos)
                {
                    lr.enabled = false;
                    continue;
                }

                Color c = _puntoVisible[i] ? Color.green : Color.red;
                lr.startColor = c;
                lr.endColor = c;
                lr.startWidth = GrosorLinea;
                lr.endWidth = GrosorLinea;
                lr.SetPosition(0, Ojos.position);
                lr.SetPosition(1, _puntos[i]);
                lr.enabled = true;
            }
        }

        /// <summary>Escribe la sospecha encima del guardia, en la vista Game.</summary>
        private void OnGUI()
        {
            if (!DepurarEnJuego || !Application.isPlaying) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Transform ojos = Ojos != null ? Ojos : transform;
            Vector3 pantalla = cam.WorldToScreenPoint(ojos.position + Vector3.up * 0.6f);
            if (pantalla.z < 0f) return;   // detrás de la cámara

            if (_estiloTexto == null)
            {
                _estiloTexto = new GUIStyle(GUI.skin.label);
                _estiloTexto.alignment = TextAnchor.MiddleCenter;
                _estiloTexto.fontStyle = FontStyle.Bold;
                _estiloTexto.fontSize = 14;
            }

            string texto = "Sospecha " + _sospecha.ToString("F2") + "  (" + _nivel + ")\n" +
                           "Rayos " + RayosVisibles + "/" + NumPuntos;

            Rect r = new Rect(pantalla.x - 110f, Screen.height - pantalla.y - 20f, 220f, 40f);

            // Sombra negra para que se lea sobre cualquier fondo.
            _estiloTexto.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), texto, _estiloTexto);

            _estiloTexto.normal.textColor = ColorNivel();
            GUI.Label(r, texto, _estiloTexto);
        }

        private Color ColorNivel()
        {
            switch (_nivel)
            {
                case NivelSospecha.Alerta: return Color.red;
                case NivelSospecha.Sospecha: return Color.yellow;
                default: return Color.white;
            }
        }

        // ───────────────────────────────────────────────────────────────────
        //  GIZMOS (solo en el editor, con el guardia seleccionado)
        // ───────────────────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            Transform ojos = Ojos != null ? Ojos : transform;

            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.6f);
            Vector3 frente = ojos.forward;
            Vector3 izq = Quaternion.AngleAxis(-AnguloVision * 0.5f, Vector3.up) * frente;
            Vector3 der = Quaternion.AngleAxis(AnguloVision * 0.5f, Vector3.up) * frente;
            Gizmos.DrawRay(ojos.position, izq * Alcance);
            Gizmos.DrawRay(ojos.position, der * Alcance);

#if UNITY_EDITOR
            UnityEditor.Handles.color = new Color(0.5f, 0.5f, 1f, 0.15f);
            UnityEditor.Handles.DrawSolidArc(ojos.position, Vector3.up, izq, AnguloVision, Alcance);
#endif

            // Zona de detección instantánea
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(ojos.position, DistanciaInstantanea);

            if (Application.isPlaying && _puntosValidos)
            {
                for (int i = 0; i < NumPuntos; i++)
                {
                    Gizmos.color = _puntoVisible[i] ? Color.green : Color.red;
                    Gizmos.DrawLine(ojos.position, _puntos[i]);
                    Gizmos.DrawSphere(_puntos[i], 0.05f);
                }
            }
        }
    }
}