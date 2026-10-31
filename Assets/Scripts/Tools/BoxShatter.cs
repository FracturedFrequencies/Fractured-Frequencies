using FracturedFrequencies.Gravity;
using FracturedFrequencies.World;
using UnityEngine;

namespace FracturedFrequencies.Tools
{
    /// <summary>
    /// Breaks a box-shaped object into a grid of physical pieces that keep falling with gravity.
    /// Code-only stand-in for Blender pre-fracturing.
    /// The original object is hidden (not destroyed) so a room reset can bring it back.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BoxShatter : MonoBehaviour, IResettable
    {
        [SerializeField] private Vector3Int pieces = new Vector3Int(3, 4, 1);
        [SerializeField] private float totalMass = 20f;
        [SerializeField] private float burstSpeed = 3f;
        [Tooltip("0 = keep the pieces until the room resets.")]
        [SerializeField] private float pieceLifetime = 8f;
        [SerializeField] private float pieceShrink = 0.92f;

        private GameObject _piecesRoot;

        public void Shatter()
        {
            if (_piecesRoot != null) Destroy(_piecesRoot);

            var box = GetComponent<BoxCollider>();
            var meshRenderer = GetComponentInChildren<MeshRenderer>();
            Material mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;

            Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
            Vector3 cell = new Vector3(size.x / pieces.x, size.y / pieces.y, size.z / pieces.z);
            Vector3 centerWorld = transform.TransformPoint(box.center);
            Quaternion rotation = transform.rotation;
            float pieceMass = totalMass / Mathf.Max(1, pieces.x * pieces.y * pieces.z);

            _piecesRoot = new GameObject(name + "_Pieces");
            var parent = _piecesRoot.transform;

            for (int x = 0; x < pieces.x; x++)
            for (int y = 0; y < pieces.y; y++)
            for (int z = 0; z < pieces.z; z++)
            {
                Vector3 local = new Vector3(
                    (x + 0.5f) * cell.x - size.x * 0.5f,
                    (y + 0.5f) * cell.y - size.y * 0.5f,
                    (z + 0.5f) * cell.z - size.z * 0.5f);
                Vector3 position = centerWorld + rotation * local;

                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "Piece";
                piece.transform.SetParent(parent, true);
                piece.transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(Random.insideUnitSphere * 10f));
                piece.transform.localScale = cell * pieceShrink;
                if (mat != null) piece.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var body = piece.AddComponent<Rigidbody>();
                body.mass = pieceMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                piece.AddComponent<GravityBody>();

                Vector3 outward = (position - centerWorld).normalized + Random.insideUnitSphere * 0.5f;
                body.AddForce(outward * burstSpeed, ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);

                if (pieceLifetime > 0f) Destroy(piece, pieceLifetime + Random.Range(0f, 1.5f));
            }

            gameObject.SetActive(false);
        }

        /// <summary>Removes leftover pieces (the room re-activates this object).</summary>
        public void ResetState()
        {
            if (_piecesRoot != null) Destroy(_piecesRoot);
            _piecesRoot = null;
        }
    }
}
