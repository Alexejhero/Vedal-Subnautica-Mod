using UnityEngine;

namespace SCHIZO.Tweaks;

internal class DevBinds : MonoBehaviour
{
    public void Update()
    {
        if (DevConsole.instance.inputField.isFocused) return;

        if (Input.GetKeyDown(KeyCode.G))
            DevConsole.SendConsoleCommand("ghost");
    }
}