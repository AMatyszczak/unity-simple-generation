using System;
using UnityEngine;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Randomizers;
using UnityEngine.Perception.Randomization.Samplers;

[RequireComponent(typeof(Light))] //Can only attach to GameObjects which also have a Light component attached
//This tag is used to "target" which objects in the scene will be randomized
public class MyLightRandomizerTag : RandomizerTag
{
    public float minIntensity;
    public float maxIntensity;

    public void SetIntensity(float rawIntensity)
    {
        var tagLight = GetComponent<Light>();
        var scaledIntensity = rawIntensity * (maxIntensity - minIntensity) + minIntensity;
        tagLight.intensity = scaledIntensity;
    }
}

[Serializable]
[AddRandomizerMenu("New Randomizer")]
public class MyLightRandomizer : Randomizer
{
    // A parameter whose value uniformly ranges from 2 to 10 when sampled
    public FloatParameter lightIntensity = new() { value = new UniformSampler(0, 1) };
    public ColorRgbParameter color;

    protected override void OnIterationStart()
    {
        // Get all MyLightRandomizerTag's in the scene
        var tags = tagManager.Query<MyLightRandomizerTag>();
        foreach (var tag in tags)
        {
            // Get the light attached to the object
            var tagLight = tag.GetComponent<Light>();            
            tagLight.color = color.Sample();
            tag.SetIntensity(lightIntensity.Sample());
        }
    }
}
