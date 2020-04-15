using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.ShaderGraph;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace UnityEditor.Rendering.HighDefinition.ShaderGraph
{
    class DecalSettingsView
    {
        HDSystemData systemData;
        DecalData decalData;

        public DecalSettingsView(HDDecalSubTarget subTarget)
        {
            systemData = subTarget.systemData;
            decalData = subTarget.decalData;
        }

        public void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange)
        {
            // TODO: Register Undo actions...

            context.AddProperty("Affects BaseColor", 0, new Toggle() { value = decalData.affectsAlbedo }, (evt) =>
            {
                if (Equals(decalData.affectsAlbedo, evt.newValue))
                    return;

                decalData.affectsAlbedo = evt.newValue;
                onChange();
            });

            context.AddProperty("Affects Normal", 0, new Toggle() { value = decalData.affectsNormal }, (evt) =>
            {
                if (Equals(decalData.affectsNormal, evt.newValue))
                    return;

                decalData.affectsNormal = evt.newValue;
                onChange();
            });

            context.AddProperty("Affects Metal", 0, new Toggle() { value = decalData.affectsMetal }, (evt) =>
            {
                if (Equals(decalData.affectsMetal, evt.newValue))
                    return;

                decalData.affectsMetal = evt.newValue;
                onChange();
            });

            context.AddProperty("Affects AO", 0, new Toggle() { value = decalData.affectsAO }, (evt) =>
            {
                if (Equals(decalData.affectsAO, evt.newValue))
                    return;

                decalData.affectsAO = evt.newValue;
                onChange();
            });

            context.AddProperty("Affects Smoothness", 0, new Toggle() { value = decalData.affectsSmoothness }, (evt) =>
            {
                if (Equals(decalData.affectsSmoothness, evt.newValue))
                    return;

                decalData.affectsSmoothness = evt.newValue;
                onChange();
            });

            context.AddProperty("Affects Emission", 0, new Toggle() { value = decalData.affectsEmission }, (evt) =>
            {
                if (Equals(decalData.affectsEmission, evt.newValue))
                    return;

                decalData.affectsEmission = evt.newValue;
                onChange();
            });
        }
    }
}
