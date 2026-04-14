using UnityEditor;
using UnityEngine;

public static class HouseBuilder
{
    [MenuItem("Tools/Create Simple House")]
    public static void CreateSimpleHouse()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        GameObject houseRoot = new GameObject("House");
        Undo.RegisterCreatedObjectUndo(houseRoot, "Create House Root");

        // Ground
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        ground.name = "Ground";
        ground.transform.SetParent(houseRoot.transform);
        ground.transform.position = new Vector3(0f, 0f, 0f);
        ground.transform.localScale = new Vector3(1f, 1f, 1f);

        // Main body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(body, "Create House Body");
        body.name = "Body";
        body.transform.SetParent(houseRoot.transform);
        body.transform.position = new Vector3(0f, 1.5f, 0f);
        body.transform.localScale = new Vector3(6f, 3f, 6f);

        // Roof
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(roof, "Create Roof");
        roof.name = "Roof";
        roof.transform.SetParent(houseRoot.transform);
        roof.transform.position = new Vector3(0f, 3.6f, 0f);
        roof.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        roof.transform.localScale = new Vector3(6.2f, 1.2f, 6.2f);

        // Door
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(door, "Create Door");
        door.name = "Door";
        door.transform.SetParent(houseRoot.transform);
        door.transform.position = new Vector3(0f, 0.9f, 3.02f);
        door.transform.localScale = new Vector3(1.2f, 1.8f, 0.15f);

        // Windows
        GameObject windowLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(windowLeft, "Create Left Window");
        windowLeft.name = "Window_Left";
        windowLeft.transform.SetParent(houseRoot.transform);
        windowLeft.transform.position = new Vector3(-1.8f, 1.8f, 3.02f);
        windowLeft.transform.localScale = new Vector3(1f, 1f, 0.12f);

        GameObject windowRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(windowRight, "Create Right Window");
        windowRight.name = "Window_Right";
        windowRight.transform.SetParent(houseRoot.transform);
        windowRight.transform.position = new Vector3(1.8f, 1.8f, 3.02f);
        windowRight.transform.localScale = new Vector3(1f, 1f, 0.12f);

        ApplyDefaultColors(body, roof, door, windowLeft, windowRight);

        Selection.activeGameObject = houseRoot;
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static void ApplyDefaultColors(
        GameObject body,
        GameObject roof,
        GameObject door,
        GameObject windowLeft,
        GameObject windowRight)
    {
        SetObjectColor(body, new Color(0.95f, 0.85f, 0.7f));
        SetObjectColor(roof, new Color(0.55f, 0.2f, 0.2f));
        SetObjectColor(door, new Color(0.35f, 0.22f, 0.12f));
        SetObjectColor(windowLeft, new Color(0.65f, 0.85f, 0.95f));
        SetObjectColor(windowRight, new Color(0.65f, 0.85f, 0.95f));
    }

    private static void SetObjectColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null)
        {
            mat = new Material(Shader.Find("Standard"));
        }

        mat.color = color;
        renderer.sharedMaterial = mat;
    }
}
