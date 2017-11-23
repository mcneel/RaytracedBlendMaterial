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
using ccl;
using RhinoCyclesCore;
using RhinoCyclesCore.ExtensionMethods;
using Rhino.Display;
using System.Runtime.InteropServices;
using static RhinoCyclesCore.Utilities;

namespace RaytracedBlendMaterial
{
	[Guid("0395385C-2787-4C41-91C5-EF8DA242F9C7")]
	public class RaytracedBlendMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Raytraced Blend Material";

		public override string TypeDescription => TypeName;

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;

		public string MaterialXml => throw new InvalidOperationException("Raytraced Blend Material is not an XML-based material");

		public float Gamma { get; set; }

		float4 Mat1 { get; set; } = new float4(0.0f);
		RenderMaterial Mat1Rm { get; set; } = null;

		float Blend { get; set; } = 0.5f;
		public bool BlendTexOn { get; set; } = false;
		public float BlendTexAmount { get; set; } = 1.0f;
		public CyclesTextureImage BlendTex { get; set; } = new CyclesTextureImage();

		float4 Mat2 { get; set; } = new float4(0.0f);
		RenderMaterial Mat2Rm { get; set; } = null;


		public RaytracedBlendMaterial()
		{
			TexturedSlot(this, "mat1", Color4f.White, "Material 1");
			TexturedSlot(this, "blend", 0.5f, "Blend factor");
			TexturedSlot(this, "mat2", Color4f.Black, "Material 2");
		}

		public void BakeParameters()
		{

			var mat1 = HandleMaterialSlot(this, "mat1");
			if(mat1.Item1)
			{
				Mat1 = mat1.Item2;
				Mat1Rm = mat1.Item5?.MakeCopy() as RenderMaterial;
			}
			var blendcolor = HandleTexturedValue(this, "blend", BlendTex);
			if (blendcolor.Item1)
			{
				Blend = blendcolor.Item2;
				BlendTexOn = blendcolor.Item3;
				BlendTexAmount = blendcolor.Item4;
			}
			var mat2 = HandleMaterialSlot(this, "mat2");
			if (mat2.Item1)
			{
				Mat2 = mat2.Item2;
				Mat2Rm = mat2.Item5?.MakeCopy() as RenderMaterial;
			}
		}

		public bool GetShader(Shader sh)
		{
			RhinoCyclesCore.Converters.ShaderConverter sconv = new RhinoCyclesCore.Converters.ShaderConverter();
			CyclesShader mat1sh = null;
			CyclesShader mat2sh = null;
			if (Mat1Rm != null)
			{
				mat1sh = sconv.CreateCyclesShader(Mat1Rm, Gamma);
				mat1sh.Gamma = Gamma;
			}
			if (Mat2Rm != null)
			{
				mat2sh = sconv.CreateCyclesShader(Mat2Rm, Gamma);
				mat2sh.Gamma = Gamma;
			}

			RhinoCyclesCore.Shaders.RhinoFullNxt fnMat1 = null;
			RhinoCyclesCore.Shaders.RhinoFullNxt fnMat2 = null;
			if(mat1sh!=null) fnMat1 = new RhinoCyclesCore.Shaders.RhinoFullNxt(null, mat1sh, sh);
			if(mat2sh!=null) fnMat2 = new RhinoCyclesCore.Shaders.RhinoFullNxt(null, mat2sh, sh);

			ccl.ShaderNodes.TextureCoordinateNode texco = new ccl.ShaderNodes.TextureCoordinateNode("texcos");
			sh.AddNode(texco);

			ccl.ShaderNodes.DiffuseBsdfNode diffuse1Bsdf = new ccl.ShaderNodes.DiffuseBsdfNode();
			diffuse1Bsdf.ins.Color.Value = Mat1;
			ccl.ShaderNodes.DiffuseBsdfNode diffuse2Bsdf = new ccl.ShaderNodes.DiffuseBsdfNode();
			diffuse2Bsdf.ins.Color.Value = Mat2;
			sh.AddNode(diffuse1Bsdf);
			sh.AddNode(diffuse2Bsdf);

			ccl.ShaderNodes.MixClosureNode blendit = new ccl.ShaderNodes.MixClosureNode("blendit");
			blendit.ins.Fac.Value = Blend;

			sh.AddNode(blendit);

			GraphForSlot(sh, BlendTexOn, BlendTex, blendit.ins.Fac, texco, true);

			var sock1 = mat1sh != null ? fnMat1.GetClosureSocket() : diffuse1Bsdf.outs.BSDF;
			sock1.Connect(blendit.ins.Closure1);
			var sock2 = mat2sh != null ? fnMat2.GetClosureSocket() : diffuse2Bsdf.outs.BSDF;
			sock2.Connect(blendit.ins.Closure2);

			blendit.outs.Closure.Connect(sh.Output.ins.Surface);

			sh.FinalizeGraph();
			return true;
		}

		public override bool IsFactoryProductAcceptableAsChild(Guid kindId, string factoryKind, string childSlotName)
		{
			if (childSlotName == "blend") return factoryKind == "texture";
			return factoryKind=="material";
		}
	}
}
