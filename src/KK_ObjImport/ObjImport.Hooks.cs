using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;


namespace ObjImport
{
    
    class Hooks
    {
        [HarmonyPostfix, HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool))]
        private static void PostfixChangeCoordinate(ChaControl __instance)
        {
            var controller = __instance.gameObject.GetComponent<CharacterController>();
            if (controller != null)
                controller.coordintateChangeEvent();
        }
    }
}
