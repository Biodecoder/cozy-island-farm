using UnityEngine;

namespace CozyFarm.World
{
    public class WindmillRotor : MonoBehaviour
    {
        public Vector3 rotationAxis = new Vector3(0f, 0f, 1f);
        public float degreesPerSecond = 38f;

        private void Update()
        {
            transform.Rotate(rotationAxis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
