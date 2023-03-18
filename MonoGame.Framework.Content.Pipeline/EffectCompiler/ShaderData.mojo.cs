using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Xna.Framework.Content.Pipeline.EffectCompiler.TPGParser;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;

// Copyright (C)2022 Nick Kastellanos

namespace Microsoft.Xna.Framework.Content.Pipeline.EffectCompiler
{
	internal partial class ShaderData
	{
        public static ShaderData CreateGLSL(byte[] byteCode, bool isVertexShader, List<ConstantBufferData> cbuffers, int sharedIndex, Dictionary<string, SamplerStateInfo> samplerStates, EffectProcessorDebugMode debugMode)
		{
            var dxshader = new ShaderData(isVertexShader, sharedIndex, byteCode);

			// Use MojoShader to convert the HLSL bytecode to GLSL.

			var parseDataPtr = MojoShader.NativeMethods.Parse(
				MojoShader.NativeConstants.PROFILE_GLSL,
				byteCode,
				byteCode.Length,
				IntPtr.Zero,
				0,
				IntPtr.Zero,
				0,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero);

			var parseData = MarshalHelper.Unmarshal<MojoShader.ParseData>(parseDataPtr);
			if (parseData.error_count > 0)
            {
				var errors = MarshalHelper.UnmarshalArray<MojoShader.Error>(
					parseData.errors,
					parseData.error_count
				);
				throw new Exception(errors [0].error);
			}

			// Conver the attributes.
			//
			// TODO: Could this be done using DX shader reflection?
			//
			{
				var attributes = MarshalHelper.UnmarshalArray<MojoShader.Attribute>(
						parseData.attributes, parseData.attribute_count);

				dxshader._attributes = new Attribute[attributes.Length];
				for (var i = 0; i < attributes.Length; i++)
                {
					dxshader._attributes [i].name = attributes [i].name;
					dxshader._attributes [i].index = attributes [i].index;
					dxshader._attributes [i].usage = EffectObject.ToXNAVertexElementUsage (attributes [i].usage);
				}
			}

			var symbols = MarshalHelper.UnmarshalArray<MojoShader.Symbol>(
					parseData.symbols, parseData.symbol_count);

			//try to put the symbols in the order they are eventually packed into the uniform arrays
			//this /should/ be done by pulling the info from mojoshader
			Array.Sort(symbols, delegate(MojoShader.Symbol a, MojoShader.Symbol b)
            {
				uint va = a.register_index;
				if (a.info.elements == 1)
					va += 1024; //hax. mojoshader puts array objects first
				uint vb = b.register_index;
				if (b.info.elements == 1)
					vb += 1024;
				return va.CompareTo(vb);
			}
			);//(a, b) => ((int)(a.info.elements > 1))a.register_index.CompareTo(b.register_index));

            // NOTE: It seems the latest versions of MojoShader only 
            // output vec4 register sets.  We leave the code below, but
            // the runtime has been optimized for this case.

			// For whatever reason the register indexing is 
			// incorrect from MojoShader.
			{
				uint bool_index = 0;
				uint float4_index = 0;
				uint int4_index = 0;

				for (var i = 0; i < symbols.Length; i++)
                {
					switch (symbols [i].register_set)
                    {
					    case MojoShader.SymbolRegisterSet.BOOL:
						    symbols [i].register_index = bool_index;
						    bool_index += symbols [i].register_count;
						    break;

					    case MojoShader.SymbolRegisterSet.FLOAT4:
						    symbols [i].register_index = float4_index;
						    float4_index += symbols[i].register_count;
						    break;

					    case MojoShader.SymbolRegisterSet.INT4:
						    symbols [i].register_index = int4_index;
						    int4_index += symbols [i].register_count;
						    break;
					}
				}
			}

			// Get the samplers.
			var samplers = MarshalHelper.UnmarshalArray<MojoShader.Sampler>(
					parseData.samplers, parseData.sampler_count);
			dxshader._samplers = new Sampler[samplers.Length];
			for (var i = 0; i < samplers.Length; i++)
            {
                // We need the original sampler name... look for that in the symbols.
                var samplerName = symbols.First(e => e.register_set == MojoShader.SymbolRegisterSet.SAMPLER &&
                                                     e.register_index == samplers[i].index
                                               ).name;

                var sampler = new Sampler
                {
                    //sampler mapping to parameter is unknown atm
                    parameter = -1,
                                      
                    // GLSL needs the MojoShader mangled sampler name.
                    samplerName = samplers[i].name,

                    // By default use the original sampler name for the parameter name.
                    parameterName = samplerName,

                    textureSlot = samplers[i].index,
                    samplerSlot = samplers[i].index,
                    type = samplers[i].type,
                };

                SamplerStateInfo state;
                if (samplerStates.TryGetValue(samplerName, out state))
                {
                    sampler.state = state.State;

                    if (state.TextureName == null)
                        sampler.parameterName = state.TextureName;

                    sampler.parameterName = state.TextureName ?? samplerName;
                }

                // Store the sampler.
			    dxshader._samplers[i] = sampler;
			}

			// Gather all the parameters used by this shader.
			var symbol_types = new [] { 
				new { name = dxshader.IsVertexShader ? "vs_uniforms_bool" : "ps_uniforms_bool", set = MojoShader.SymbolRegisterSet.BOOL, },
				new { name = dxshader.IsVertexShader ? "vs_uniforms_ivec4" : "ps_uniforms_ivec4", set = MojoShader.SymbolRegisterSet.INT4, },
				new { name = dxshader.IsVertexShader ? "vs_uniforms_vec4" : "ps_uniforms_vec4", set = MojoShader.SymbolRegisterSet.FLOAT4, },
			};

			var cbuffer_index = new List<int>();
			for (var i = 0; i < symbol_types.Length; i++)
            {
				var cbuffer = new ConstantBufferData(symbol_types [i].name,
													 symbol_types [i].set,
													 symbols);
				if (cbuffer.Size == 0)
					continue;

				var match = cbuffers.FindIndex(e => e.SameAs(cbuffer));
				if (match == -1)
                {
					cbuffer_index.Add(cbuffers.Count);
					cbuffers.Add(cbuffer);
				}
                else
					cbuffer_index.Add(match);
			}
			dxshader._cbuffers = cbuffer_index.ToArray();

			var glslCode = parseData.output;

			// TODO: This sort of sucks... why does MojoShader not produce
			// code valid for GLES out of the box?

			// GLES platforms do not like this.
			glslCode = glslCode.Replace("#version 110", "");

			// Add the required precision specifiers for GLES.

            var floatPrecision = dxshader.IsVertexShader ? "precision highp float;\r\n" : "precision mediump float;\r\n";

			glslCode = "#ifdef GL_ES\r\n" +
                 floatPrecision +
				"precision mediump int;\r\n" +
				"#endif\r\n" +
				glslCode;

			// Enable standard derivatives extension as necessary
			if (glslCode.IndexOf("dFdx", StringComparison.InvariantCulture) >= 0
			||  glslCode.IndexOf("dFdy", StringComparison.InvariantCulture) >= 0)
			{
				glslCode = "#extension GL_OES_standard_derivatives : enable\r\n" + glslCode;
			}

			// Store the code for serialization.
			dxshader.ShaderCode = Encoding.ASCII.GetBytes(glslCode);

            return dxshader;
		}
	}
}
