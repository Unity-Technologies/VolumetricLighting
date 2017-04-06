Volumetric fog, area lights and tube lights
===========================================

This repository contains some of the lighting effects implemented for the [Unity Adam demo](https://unity3d.com/pages/adam): volumetric fog, area lights and tube lights.

##### Adam
![fog in the Adam demo](http://i.imgur.com/XYExw51.gif)

##### Test scenes
![area light](http://i.imgur.com/HxwAefB.png)
Area light with shadows.

![tube light](http://i.imgur.com/MxS8Jus.png)
Two tube lights with some locally controlled fog density.

![tube light](http://i.imgur.com/ZSXoCYX.png)
Tube light with a shadow plane and global fog noise + wind.

![transparency](http://i.imgur.com/Yr8Zj2S.png)
Transparent spheres and a point light.

System requirements
-------------------
- DX11
- Unity 5.6

Usage notes
-----------
##### Volumetric fog
- The final outcome of light scattering in the fog depends on three factors: fog density, fog anisotropy and light sources.
- To affect the fog, an area light, tube light or point light needs to have the FogLight component and the scene needs an instance of LightManagerFogLights.
- Global fog density controls on the VolumetricFog component allow defining constant fog density, exponential height-based and animated noise.
- Density can also be controlled locally with the FogEllipsoid shapes.
- Anisotropy is a global property on the VolumetricFog component. It allows the light to scatter forward more than in other directions, which is characteristic for Mie scattering occuring with larger particles like water droplets of fog.

##### Area lights
- Area lights give great direct light results, but their shadows and scattering in the fog are very rough approximations in this project.
- Direct light soft shadows are implemented using PCSS - the technique was good enough for our specific cameras, but in general isn't great and the settings are difficult to tweak. Real time shadows from area lights is still an open research topic.
- Regular area light should contribute to the scene in its entire front hemisphere. This is difficult to handle for a shadowmap (paraboloidal projection and multiple shadowmaps are not very practical), so with shadows enabled, area light's influence gets limited to a frustum with a controllable angle.
- The scattering contribution in the fog is limited to the same frustum and not physically correct either.
- Volumetric shadows in the fog use Variance Shadow Maps. VSM allows for pre-blurring the shadowmap for very soft shadows with wide penumbra for the cost of just one shadow sample at the stage of injecting the light into the froxel volume.
- Softness of the volumetric shadows, apart from looking more natural, has an important role: it avoids aliasing by changing sharp shadow edges into smoother gradients, this way pushing the gradient above the Nyquist freqency of the froxel resolution. In other words: if shadows are aliasing, try blurring them more in the *FogLight* settings.
- The original paper uses Exponential Shadow Maps. ESMs cause light leaking right behind the shadow caster; on top of that, their penumbra is very wide right behind the shadow caster, but gets narrow further away. Those two properties made them very hard to tweak for our shots not to be washed out, avoid aliasing, etc. VSMs don't leak light this way and have a more constant penumbra, so were a better fit for us. (VSMs experience a different kind of light leaking in a specific configuration of shadow casters and receivers, but we didn't have that problem in our scene).

##### Tube lights
- Tube lights use a very cheap and practical solution to approximate area lights, but the resulting quality is much lower.
- Still a very useful fill light, source of extra specular highlights or simply when there's a need for an elongated, omnidirectional light.
- Tube lights don't have proper shadows, but each one can have two *ShadowPlanes*. Shadow planes cut off the light's influence and have a controllable feather.

Known issues and missing functionality
--------------------------------------
- Directional light support disabled for now, but should be added soon.
- The froxel resolution is currently hardcoded and larger view distances will cause undersampling issues along the view axis. Should be fixed soon. In the meantime use the *Far Clip Max* setting to limit how far the volumetrics are calculated.
- The lights only work in deferred rendering and won't affect any objects rendered in forward.
- No scaling via the transform component - currently the size and shape of the lights can only be changed via the properties of the light component. Changing the scale in the transform component doesn't work.
- Currently the lights always render as back faces of the proxy mesh, without using the stencil buffer. A proper deferred light needs to be rendered as front faces, back faces or a quad (depending on the intersections with the near and far plane) and cull pixels using stencil. Not implemented for Adam, since wouldn't have changed anything with our specific cameras and light setup, but necessary for the lights to be universal.
- The material emission of the light source mesh (the quad and the capsule) is only a rough approximation - needs to be calculated properly.
- Banding - there's quite a bit of banding, particularly visible in those dark scenes above; needs to be solved somehow. Note that banding isn't the same as aliasing. The latter creates differently looking bands and happens due to the froxel resolution being too low (below the Nyquist frequency) for the sharpness of features in the fog (shadow edges, quick changing gradients). To fix aliasing, increase froxel resolution (controls coming, or decrease *Far Clip Max*), or make the features in the fog smoother (e.g. by blurring the shadows more).
- Area light's have Unity's standard shader BRDF *baked* into it's lookup tables. To make them work with a different shader, it's BRDF would have to baked into new lookup tables. There's currently no tool to do that, but we should make one. In the meantime take a look at the original paper in the references.


Contact
-------
Please use GitHub's issue tracker to file any bugs.

If you have any questions, it's best to reach Robert via twitter: [@robertcupisz](https://twitter.com/robertcupisz)

References
----------
- Volumetric fog based on [Volumetric fog: Unified, compute shader based solution to atmospheric scattering, ACM Siggraph 2014](https://bartwronski.com/publications/), Bart Wronski (Ubisoft)
- Area lights based on [Real-Time Polygonal-Light Shading with Linearly Transformed Cosines](https://labs.unity.com/article/real-time-polygonal-light-shading-linearly-transformed-cosines), Eric Heitz (Unity Technologies), Jonathan Dupuy (Unity Technologies), Stephen Hill (Ubisoft) and David Neubelt (Ready At Dawn Studios)
- Tube lights based on [Real Shading in Unreal Engine 4](http://blog.selfshadow.com/publications/s2013-shading-course/#course_content), Brian Karis (Epic Games)

Some more Adam demo shots

![fog in the Adam demo](http://i.imgur.com/3ZZk7cL.png)
![fog in the Adam demo](http://i.imgur.com/QfIeIYR.png)

