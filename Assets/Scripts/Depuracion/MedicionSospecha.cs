// MedicionSospecha.cs — Herramienta de medida para el apartado G de la memoria.
// Se pone en el GUARDIA (junto a SensorVision). Solo LEE los datos públicos del
// sensor y escribe en la consola: no interviene en la percepción ni en la decisión.
//
// Cada línea [MEDIDA] se puede copiar tal cual a la tabla de pruebas.

using UnityEngine;

namespace UDIT.IA3.Guardia
{
    [RequireComponent(typeof(SensorVision))]
    public class MedicionSospecha : MonoBehaviour
    {
        [Header("Rayos de visión")]
        [Tooltip("Dibuja los 5 rayos con gizmos (vista Scene, o Game con Gizmos activado): " +
                 "verde = llega al punto, rojo = tapado.")]
        public bool DibujarRayos = true;

        [Tooltip("Dibuja la esfera de DistanciaMaxEvidencia (a esa distancia o menos, " +
                 "el factor distancia vale 1).")]
        public bool DibujarDistanciaMaxEvidencia = true;

        [Header("Depuración en juego (vista Game)")]
        [Tooltip("Dibuja los 5 rayos con líneas reales, visibles en la vista Game " +
                 "aunque los gizmos estén desactivados.")]
        public bool RayosEnJuego = true;

        [Tooltip("Muestra el valor de sospecha escrito encima del guardia.")]
        public bool TextoEnJuego = true;

        [Tooltip("Grosor de las líneas de los rayos (en metros).")]
        public float GrosorLinea = 0.03f;

        [Tooltip("Además, escribe la sospecha en la consola cada frame (líneas [SOSPECHA], " +
                 "distintas de las [MEDIDA]). Puede ser mucho ruido.")]
        public bool LogSospechaEnConsola = false;

        private SensorVision _sensor;

        private bool _midiendo;
        private float _t0;
        private float _tPerdido = -1f;
        private int _rayosPrevios;
        private NivelSospecha _nivelPrevio = NivelSospecha.Tranquilo;

        private const int NumPuntos = 5;
        private readonly Vector3[] _puntos = new Vector3[NumPuntos];
        private readonly bool[] _puntoVisible = new bool[NumPuntos];
        private bool _puntosValidos;

        private LineRenderer[] _lineas;
        private GUIStyle _estiloTexto;

        private void Awake()
        {
            _sensor = GetComponent<SensorVision>();

            if (RayosEnJuego) CrearLineas();
        }

        private void Update()
        {
            int rayos = _sensor.RayosVisibles;
            NivelSospecha nivel = _sensor.NivelActual;

            // Empieza una medida cuando el guardia pasa de no ver nada a ver algo
            if (rayos > 0 && _rayosPrevios == 0 && !_midiendo)
            {
                _midiendo = true;
                _t0 = Time.time;
                _tPerdido = -1f;
                Debug.Log("[MEDIDA] Empieza: distancia " + Distancia().ToString("F1") +
                          " m, ángulo " + Angulo().ToString("F0") +
                          " grados, sospecha inicial " + _sensor.SospechaActual.ToString("F2"));
            }

            // Cada cambio de nivel, con el tiempo desde el inicio
            if (_midiendo && nivel != _nivelPrevio)
            {
                Debug.Log("[MEDIDA] " + _nivelPrevio + " -> " + nivel + " a los " +
                          (Time.time - _t0).ToString("F2") + " s (sospecha " +
                          _sensor.SospechaActual.ToString("F2") + ")");
            }

            // Lo pierde de vista: empieza a contar el olvido
            if (_midiendo && rayos == 0 && _rayosPrevios > 0)
                _tPerdido = Time.time;

            // Vuelve a verlo mientras olvidaba: se anula el conteo de olvido
            if (_midiendo && rayos > 0 && _rayosPrevios == 0 && _tPerdido >= 0f)
                _tPerdido = -1f;

            // Ha olvidado del todo: termina la medida.
            if (_midiendo && rayos == 0 && nivel == NivelSospecha.Tranquilo && _tPerdido >= 0f)
            {
                Debug.Log("[MEDIDA] Olvido completo a los " +
                          (Time.time - _tPerdido).ToString("F2") + " s desde que lo perdió");
                _midiendo = false;
            }

            _rayosPrevios = rayos;
            _nivelPrevio = nivel;
        }

        private float Distancia()
        {
            if (_sensor.Objetivo == null) return 0f;
            return Vector3.Distance(transform.position, _sensor.Objetivo.position);
        }

        private float Angulo()
        {
            if (_sensor.Objetivo == null) return 0f;
            Transform ojos = _sensor.Ojos != null ? _sensor.Ojos : transform;

            Vector3 hacia = _sensor.Objetivo.position - ojos.position;
            Vector3 frente = ojos.forward;
            hacia.y = 0f;
            frente.y = 0f;
            if (hacia.sqrMagnitude < 0.0001f || frente.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Angle(frente, hacia);
        }

        // ───────────────────────────────────────────────────────────────────
        //  PUNTOS Y RAYCASTS (solo para dibujar)
        // ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Repite lo que hace SensorVision (alcance, cono y 5 raycasts) pero solo
        /// para dibujar. Si el objetivo está fuera del alcance o del cono no se
        /// lanza ningún rayo, igual que en el sensor.
        /// </summary>
        private void RecalcularPuntos()
        {
            _puntosValidos = false;

            Transform objetivo = _sensor.Objetivo;
            Transform ojos = _sensor.Ojos != null ? _sensor.Ojos : transform;
            if (objetivo == null) return;

            Vector3 centro = objetivo.position + Vector3.up * _sensor.AlturaCentro;
            Vector3 haciaObjetivo = centro - ojos.position;

            // Alcance
            if (haciaObjetivo.sqrMagnitude > _sensor.Alcance * _sensor.Alcance) return;

            // Ángulo
            Vector3 dirAngulo = haciaObjetivo;
            Vector3 frente = ojos.forward;
            dirAngulo.y = 0f;
            frente.y = 0f;

            if (dirAngulo.sqrMagnitude > 0.0001f && frente.sqrMagnitude > 0.0001f)
            {
                if (Vector3.Angle(frente, dirAngulo) > _sensor.AnguloVision * 0.5f) return;
            }

            // Los 5 puntos
            Vector3 lado = Vector3.Cross(Vector3.up, dirAngulo);
            lado = lado.sqrMagnitude > 0.0001f ? lado.normalized : ojos.right;

            Vector3 basePos = objetivo.position;
            _puntos[0] = centro;
            _puntos[1] = basePos + Vector3.up * _sensor.AlturaCabeza;
            _puntos[2] = basePos + Vector3.up * _sensor.AlturaPies;
            _puntos[3] = centro - lado * _sensor.SeparacionLateral;
            _puntos[4] = centro + lado * _sensor.SeparacionLateral;

            for (int i = 0; i < NumPuntos; i++)
                _puntoVisible[i] = PuntoVisible(ojos.position, _puntos[i]);

            _puntosValidos = true;
        }

        /// <summary>TRUE si el raycast al punto NO choca contra ninguna capa que ocluya.</summary>
        private bool PuntoVisible(Vector3 origen, Vector3 punto)
        {
            Vector3 dir = punto - origen;
            float dist = dir.magnitude;
            if (dist < 0.001f) return true;

            return !Physics.Raycast(origen, dir / dist, dist - 0.02f, _sensor.CapasQueOcluyen,
                                    QueryTriggerInteraction.Ignore);
        }

        // ───────────────────────────────────────────────────────────────────
        //  DEPURACIÓN EN JUEGO (vista Game)
        // ───────────────────────────────────────────────────────────────────

        private static Color ColorDe(NivelSospecha n)
        {
            switch (n)
            {
                case NivelSospecha.Alerta: return new Color(1f, 0.2f, 0.2f);
                case NivelSospecha.Sospecha: return new Color(1f, 0.85f, 0.2f);
                default: return Color.white;
            }
        }

        private void LateUpdate()
        {
            if (RayosEnJuego || DibujarRayos) RecalcularPuntos();
            if (RayosEnJuego) ActualizarLineas();

            if (LogSospechaEnConsola)
            {
                Debug.Log("[SOSPECHA] " + name + " · " + _sensor.SospechaActual.ToString("F2") +
                          " (" + _sensor.NivelActual + ") · rayos " + _sensor.RayosVisibles +
                          "/" + NumPuntos, this);
            }
        }

        /// <summary>Crea un LineRenderer por cada punto para visualmente ver los raycast</summary>
        private void CrearLineas()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            Material mat = new Material(shader);
            _lineas = new LineRenderer[NumPuntos];

            for (int i = 0; i < _lineas.Length; i++)
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

        /// <summary>
        /// Verde, el rayo llega al punto, rojo algo lo tapa. Si el jugador está fuera del alcance o del cono no se lanza ningún rayo,  no dibuja nada.
        /// </summary>
        private void ActualizarLineas()
        {
            if (_lineas == null) return;

            Transform ojos = _sensor.Ojos != null ? _sensor.Ojos : transform;

            for (int i = 0; i < _lineas.Length; i++)
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
                lr.SetPosition(0, ojos.position);
                lr.SetPosition(1, _puntos[i]);
                lr.enabled = true;
            }
        }

        /// <summary>Escribe la sospecha en el juego</summary>
        private void OnGUI()
        {
            if (!TextoEnJuego || !Application.isPlaying || _sensor == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Transform ojos = _sensor.Ojos != null ? _sensor.Ojos : transform;
            Vector3 pantalla = cam.WorldToScreenPoint(ojos.position + Vector3.up * 0.6f);
            if (pantalla.z < 0f) return;

            if (_estiloTexto == null)
            {
                _estiloTexto = new GUIStyle(GUI.skin.label);
                _estiloTexto.alignment = TextAnchor.MiddleCenter;
                _estiloTexto.fontStyle = FontStyle.Bold;
                _estiloTexto.fontSize = 14;
            }

            string texto = "Sospecha " + _sensor.SospechaActual.ToString("F2") +
                           "  (" + _sensor.NivelActual + ")\n" +
                           "Rayos " + _sensor.RayosVisibles + "/" + NumPuntos;

            Rect r = new Rect(pantalla.x - 110f, Screen.height - pantalla.y - 20f, 220f, 40f);

            _estiloTexto.normal.textColor = ColorDe(_sensor.NivelActual);
            GUI.Label(r, texto, _estiloTexto);
        }

        // ───────────────────────────────────────────────────────────────────
        //  GIZMOS DE LOS RAYOS
        // ───────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (_sensor == null) _sensor = GetComponent<SensorVision>();
            if (_sensor == null) return;

            Transform ojos = _sensor.Ojos != null ? _sensor.Ojos : transform;

            if (DibujarDistanciaMaxEvidencia)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawWireSphere(ojos.position, _sensor.DistanciaMaxEvidencia);
            }

            // Los 5 rayos: verde llega, rojo tapado
            if (DibujarRayos && Application.isPlaying && _puntosValidos)
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