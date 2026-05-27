using UnityEngine;

namespace GroceryQuotaHorror.Data
{
    public static class PrototypeVisualMaterials
    {
        private const string DefaultLitResourcePath = "Materials/PrototypeDefaultLit";
        private static Material defaultLit;

        public static void ApplyDefaultLit(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            defaultLit ??= Resources.Load<Material>(DefaultLitResourcePath);
            if (defaultLit != null)
            {
                renderer.sharedMaterial = defaultLit;
            }
        }
    }
}
