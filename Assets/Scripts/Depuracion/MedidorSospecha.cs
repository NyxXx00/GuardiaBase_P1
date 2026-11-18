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
        private SensorVision _sensor;

        private bool _midiendo;
        private float _t0;
        private float _tPerdido = -1f;
        private int _rayosPrevios;
        private NivelSospecha _nivelPrevio = NivelSospecha.Tranquilo;

        private void Awake()
        {
            _sensor = GetComponent<SensorVision>();
        }

        private void Update()
        {
            int rayos = _sensor.RayosVisibles;
            NivelSospecha nivel = _sensor.NivelActual;

            // Empieza una medida cuando el guardia pasa de no ver nada a ver algo.
            if (rayos > 0 && _rayosPrevios == 0 && !_midiendo)
            {
                _midiendo = true;
                _t0 = Time.time;
                _tPerdido = -1f;
                Debug.Log("[MEDIDA] Empieza: distancia " + Distancia().ToString("F1") +
                          " m, ángulo " + Angulo().ToString("F0") +
                          " grados, sospecha inicial " + _sensor.SospechaActual.ToString("F2"));
            }

            // Cada cambio de nivel, con el tiempo desde el inicio.
            if (_midiendo && nivel != _nivelPrevio)
            {
                Debug.Log("[MEDIDA] " + _nivelPrevio + " -> " + nivel + " a los " +
                          (Time.time - _t0).ToString("F2") + " s (sospecha " +
                          _sensor.SospechaActual.ToString("F2") + ")");
            }

            // Lo pierde de vista: empieza a contar el olvido.
            if (_midiendo && rayos == 0 && _rayosPrevios > 0)
                _tPerdido = Time.time;

            // Vuelve a verlo mientras olvidaba: se anula el conteo de olvido.
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
    }
}