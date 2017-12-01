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
using Rhino.Display;
using System.Runtime.InteropServices;
using static RhinoCyclesCore.Utilities;
using ccl.ShaderNodes.Sockets;

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

		static int running_serial = 0;

		private int Serial { get; set; }

		public RaytracedBlendMaterial()
		{
			Serial = running_serial++;
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
				if(Mat1Rm is ICyclesMaterial crm1)  crm1.BakeParameters();
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
				if(Mat2Rm is ICyclesMaterial crm2)  crm2.BakeParameters();
			}
		}


		ccl.ShaderNodes.MixClosureNode blendit;
		public bool GetShader(Shader sh, bool finalize)
		{
			blendit = new ccl.ShaderNodes.MixClosureNode($"blendit{Serial}");
			sh.AddNode(blendit);

			RhinoCyclesCore.Converters.ShaderConverter sconv = new RhinoCyclesCore.Converters.ShaderConverter();
			CyclesShader mat1sh = null;
			CyclesShader mat2sh = null;
			ICyclesMaterial crm1 = null;
			ICyclesMaterial crm2 = null;
			ClosureSocket crm1closure = null;
			ClosureSocket crm2closure = null;
			if (Mat1Rm != null)
			{
				if (Mat1Rm is ICyclesMaterial)
				{
					crm1 = Mat1Rm as ICyclesMaterial;
					crm1.Gamma = Gamma;
					crm1.GetShader(sh, false);
					crm1closure = crm1.GetClosureSocket(sh);
				}
				else
				{
					mat1sh = sconv.CreateCyclesShader(Mat1Rm, Gamma);
					mat1sh.Gamma = Gamma;
					RhinoCyclesCore.Converters.BitmapConverter.ReloadTextures(mat1sh);
				}
			}
			if (Mat2Rm != null)
			{
				if (Mat2Rm is ICyclesMaterial)
				{
					crm2 = Mat2Rm as ICyclesMaterial;
					crm2.Gamma = Gamma;
					crm2.GetShader(sh, false);
					crm2closure = crm2.GetClosureSocket(sh);
				}
				else
				{
					mat2sh = sconv.CreateCyclesShader(Mat2Rm, Gamma);
					mat2sh.Gamma = Gamma;
					RhinoCyclesCore.Converters.BitmapConverter.ReloadTextures(mat2sh);
				}
			}

			RhinoCyclesCore.Shaders.RhinoFullNxt fnMat1 = null;
			ClosureSocket fnMat1Closure = null;
			if (mat1sh != null)
			{
				fnMat1 = new RhinoCyclesCore.Shaders.RhinoFullNxt(null, mat1sh, sh, false);
				fnMat1Closure = fnMat1.GetClosureSocket();
			}

			RhinoCyclesCore.Shaders.RhinoFullNxt fnMat2 = null;
			ClosureSocket fnMat2Closure = null;
			if (mat2sh != null)
			{
				fnMat2 = new RhinoCyclesCore.Shaders.RhinoFullNxt(null, mat2sh, sh, false);
				fnMat2Closure = fnMat2.GetClosureSocket();
			}

			ccl.ShaderNodes.TextureCoordinateNode texco = new ccl.ShaderNodes.TextureCoordinateNode($"texcos{Serial}");
			sh.AddNode(texco);

			ccl.ShaderNodes.DiffuseBsdfNode diffuse1Bsdf = new ccl.ShaderNodes.DiffuseBsdfNode();
			diffuse1Bsdf.ins.Color.Value = Mat1;
			ccl.ShaderNodes.DiffuseBsdfNode diffuse2Bsdf = new ccl.ShaderNodes.DiffuseBsdfNode();
			diffuse2Bsdf.ins.Color.Value = Mat2;
			sh.AddNode(diffuse1Bsdf);
			sh.AddNode(diffuse2Bsdf);

			blendit.ins.Fac.Value = Blend;

			GraphForSlot(sh, BlendTexOn, BlendTex, blendit.ins.Fac, texco, true);

			var sock1 = fnMat1Closure ?? crm1closure ?? diffuse1Bsdf.outs.BSDF;
			sock1.Connect(blendit.ins.Closure1);
			var sock2 = fnMat2Closure ?? crm2closure ?? diffuse2Bsdf.outs.BSDF;
			sock2.Connect(blendit.ins.Closure2);

			blendit.outs.Closure.Connect(sh.Output.ins.Surface);

			if(finalize) sh.FinalizeGraph();
			return true;
		}

		public override bool IsFactoryProductAcceptableAsChild(Guid kindId, string factoryKind, string childSlotName)
		{
			if (childSlotName == "blend") return factoryKind == "texture";
			return factoryKind=="material";
		}

		public override void SimulateMaterial(ref Rhino.DocObjects.Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			if (Fields.TryGetValue("mat1", out Color4f color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();
		}

		public ClosureSocket GetClosureSocket(Shader sh)
		{
			return blendit.outs.Closure;
		}
	}
}
