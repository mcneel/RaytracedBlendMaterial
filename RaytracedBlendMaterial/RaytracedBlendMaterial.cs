/**
Copyright 2017 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using Rhino.Render;
using RhinoCyclesCore.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ccl;
using RhinoCyclesCore;
using RhinoCyclesCore.ExtensionMethods;
using Rhino.Display;

namespace RaytracedBlendMaterial
{
	public class RaytracedBlendMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Raytraced Blend Material";

		public override string TypeDescription => TypeName;

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;

		public string MaterialXml => throw new InvalidOperationException("Raytraced Blend Material is not an XML-based material");

		public float Gamma { get; set; }

		public RaytracedBlendMaterial()
		{
			Fields.AddTextured("mat1", Color4f.White, "Material 1");
			Fields.AddTextured("blend", 0.5f, "Blend factor");
			Fields.AddTextured("mat2", null, "Material 2");
		}

		public void BakeParameters()
		{
		}

		public bool GetShader(Shader sh)
		{
			ccl.ShaderNodes.DiffuseBsdfNode diffuseBsdf = new ccl.ShaderNodes.DiffuseBsdfNode();
			diffuseBsdf.ins.Color.Value = Color4f.White.ToFloat4();
			sh.AddNode(diffuseBsdf);
			diffuseBsdf.outs.BSDF.Connect(sh.Output.ins.Surface);

			return true;
		}
	}
}
