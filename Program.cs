using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Device11 = SharpDX.Direct3D11.Device;
using Buffer11 = SharpDX.Direct3D11.Buffer;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;

namespace TestSharpDX
{
	static class Program
	{
		[STAThread]
		static void Main()
		{
			using (var form = new RenderForm())
			using (var factory = new Factory4())
			{
				Device11 device;
				SwapChain swapChain;

				Device11.CreateWithSwapChain(
					DriverType.Hardware,
					DeviceCreationFlags.None,
					new SwapChainDescription
					{
						IsWindowed = true,	
						BufferCount = 1,
						OutputHandle = form.Handle,
						SampleDescription = new SampleDescription(1, 0),
						ModeDescription = new ModeDescription(form.ClientSize.Width, form.ClientSize.Height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
						Usage = Usage.RenderTargetOutput,
						SwapEffect = SwapEffect.Discard,
						Flags = SwapChainFlags.None
					},
					out device,
					out swapChain);

				var context = device.ImmediateContext;

				var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);

				var backBufferView = new RenderTargetView(device, backBuffer);

				backBuffer.Dispose();

				var depthBuffer = new Texture2D(device, new Texture2DDescription
				{
					Format = Format.D16_UNorm,
					ArraySize = 1,
					MipLevels = 1,
					Width = form.ClientSize.Width,
					Height = form.ClientSize.Height,
					SampleDescription = new SampleDescription(1, 0),
					Usage = ResourceUsage.Default,
					BindFlags = BindFlags.DepthStencil,
					CpuAccessFlags = CpuAccessFlags.None,
					OptionFlags = ResourceOptionFlags.None
				});

				var depthBufferView = new DepthStencilView(device, depthBuffer);

				depthBuffer.Dispose();

				//Indices
				var indices = new int[]
				{
					0,1,2,0,2,3,
					4,6,5,4,7,6,
					8,9,10,8,10,11,
					12,14,13,12,15,14,
					16,18,17,16,19,18,
					20,21,22,20,22,23
				};

				//Vertices
				var vertices = new[]
				{
					////TOP
					new ColoredVertex(new Vector3(-5,5,5),new Vector4(0,1,0,0)),
					new ColoredVertex(new Vector3(5,5,5),new Vector4(0,1,0,0)),
					new ColoredVertex(new Vector3(5,5,-5),new Vector4(0,1,0,0)),
					new ColoredVertex(new Vector3(-5,5,-5),new Vector4(0,1,0,0)),
					//BOTTOM
					new ColoredVertex(new Vector3(-5,-5,5),new Vector4(1,0,1,1)),
					new ColoredVertex(new Vector3(5,-5,5),new Vector4(1,0,1,1)),
					new ColoredVertex(new Vector3(5,-5,-5),new Vector4(1,0,1,1)),
					new ColoredVertex(new Vector3(-5,-5,-5),new Vector4(1,0,1,1)),
					//LEFT
					new ColoredVertex(new Vector3(-5,-5,5),new Vector4(1,0,0,1)),
					new ColoredVertex(new Vector3(-5,5,5),new Vector4(1,0,0,1)),
					new ColoredVertex(new Vector3(-5,5,-5),new Vector4(1,0,0,1)),
					new ColoredVertex(new Vector3(-5,-5,-5),new Vector4(1,0,0,1)),
					//RIGHT
					new ColoredVertex(new Vector3(5,-5,5),new Vector4(1,1,0,1)),
					new ColoredVertex(new Vector3(5,5,5),new Vector4(1,1,0,1)),
					new ColoredVertex(new Vector3(5,5,-5),new Vector4(1,1,0,1)),
					new ColoredVertex(new Vector3(5,-5,-5),new Vector4(1,1,0,1)),
					//FRONT
					new ColoredVertex(new Vector3(-5,5,5),new Vector4(0,1,1,1)),
					new ColoredVertex(new Vector3(5,5,5),new Vector4(0,1,1,1)),
					new ColoredVertex(new Vector3(5,-5,5),new Vector4(0,1,1,1)),
					new ColoredVertex(new Vector3(-5,-5,5),new Vector4(0,1,1,1)),
					//BACK
					new ColoredVertex(new Vector3(-5,5,-5),new Vector4(0,0,1,1)),
					new ColoredVertex(new Vector3(5,5,-5),new Vector4(0,0,1,1)),
					new ColoredVertex(new Vector3(5,-5,-5),new Vector4(0,0,1,1)),
					new ColoredVertex(new Vector3(-5,-5,-5),new Vector4(0,0,1,1))
				};

				var vertexBuffer = Buffer11.Create(device, BindFlags.VertexBuffer, vertices);
				var indexBuffer = Buffer11.Create(device, BindFlags.IndexBuffer, indices);

				var vertexBufferBinding = new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<ColoredVertex>(), 0);

				var source = @"
cbuffer data :register(b0)
{
	float4x4 worldViewProj;
};

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct PS_IN
{
	float4 position : SV_POSITION;
	float4 color : COLOR;
};

PS_IN VS( VS_IN input)
{
	PS_IN output = (PS_IN)0;

	output.position = mul(worldViewProj,input.position);
	output.color=input.color;

	return output;
}

float4 PS( PS_IN input ) : SV_Target
{
	return input.color;
}
";
				var vertexShaderByteCode = ShaderBytecode.Compile(source, "VS", "vs_5_0");
				var vertexShader = new VertexShader(device, vertexShaderByteCode);
				var pixelShader = new PixelShader(device, ShaderBytecode.Compile(source, "PS", "ps_5_0"));

				var layout = new InputLayout(device, ShaderSignature.GetInputSignature(vertexShaderByteCode), new InputElement[]
				{
					new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
					new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 12, 0)
				});

				var worldViewProjectionBuffer = new Buffer11(device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

				var rasterizerStateDescription = RasterizerStateDescription.Default();
				var rasterizerState = new RasterizerState(device, rasterizerStateDescription);

				var blendStateDescription = BlendStateDescription.Default();
				var blendState = new BlendState(device, blendStateDescription);

				var depthStateDescription = DepthStencilStateDescription.Default();

				depthStateDescription.DepthComparison = Comparison.LessEqual;
				depthStateDescription.IsDepthEnabled = true;
				depthStateDescription.IsStencilEnabled = false;

				var depthStencilState = new DepthStencilState(device, depthStateDescription);

				var samplerStateDescription = SamplerStateDescription.Default();

				samplerStateDescription.Filter = Filter.MinMagMipLinear;
				samplerStateDescription.AddressU = TextureAddressMode.Wrap;
				samplerStateDescription.AddressV = TextureAddressMode.Wrap;

				var samplerState = new SamplerState(device, samplerStateDescription);

				var startTime = DateTime.Now;
				var frame = 0;
				var size = form.ClientSize;

				RenderLoop.Run(form, () =>
				{
					if (form.ClientSize != size)
					{
						Utilities.Dispose(ref backBufferView);
						Utilities.Dispose(ref depthBufferView);

						if (form.ClientSize.Width != 0 && form.ClientSize.Height != 0)
						{
							swapChain.ResizeBuffers(1, form.ClientSize.Width, form.ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);

							backBuffer = swapChain.GetBackBuffer<Texture2D>(0);

							backBufferView = new RenderTargetView(device, backBuffer);
							backBuffer.Dispose();

							depthBuffer = new Texture2D(device, new Texture2DDescription
							{
								Format = Format.D16_UNorm,
								ArraySize = 1,
								MipLevels = 1,
								Width = form.ClientSize.Width,
								Height = form.ClientSize.Height,
								SampleDescription = new SampleDescription(1, 0),
								Usage = ResourceUsage.Default,
								BindFlags = BindFlags.DepthStencil,
								CpuAccessFlags = CpuAccessFlags.None,
								OptionFlags = ResourceOptionFlags.None
							});

							depthBufferView = new DepthStencilView(device, depthBuffer);
							depthBuffer.Dispose();
						}

						size = form.ClientSize;
					}

					var ratio = (float)form.ClientSize.Width / (float)form.ClientSize.Height;

					var projection = Matrix.PerspectiveFovLH(3.14F / 3.0F, ratio, 1, 1000);
					var view = Matrix.LookAtLH(new Vector3(0, 10, -50), Vector3.Zero, Vector3.UnitY);
					var world = Matrix.RotationY(Environment.TickCount / 1000.0F);

					var worldViewProjection = world * view * projection;

					//worldViewProjection = Matrix.Identity;

					context.UpdateSubresource(ref worldViewProjection, worldViewProjectionBuffer);

					context.VertexShader.SetConstantBuffer(0, worldViewProjectionBuffer);

					context.Rasterizer.SetViewport(0, 0, form.ClientSize.Width, form.ClientSize.Height);
					context.OutputMerger.SetTargets(depthBufferView, backBufferView);

					context.ClearRenderTargetView(backBufferView, new RawColor4(0, 0, 0, 1));
					context.ClearDepthStencilView(depthBufferView, DepthStencilClearFlags.Depth, 1.0f, 0);

					context.InputAssembler.InputLayout = layout;
					context.InputAssembler.SetVertexBuffers(0, vertexBufferBinding);
					context.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, 0);
					context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
					context.VertexShader.Set(vertexShader);
					context.PixelShader.Set(pixelShader);
					context.GeometryShader.Set(null);
					context.DomainShader.Set(null);
					context.HullShader.Set(null);

					context.Rasterizer.State = rasterizerState;
					context.OutputMerger.SetBlendState(blendState);
					context.OutputMerger.SetDepthStencilState(depthStencilState);
					context.PixelShader.SetSampler(0, samplerState);

					context.DrawIndexed(indices.Length, 0, 0);

					swapChain.Present(0, PresentFlags.None);

					frame++;
				});

				MessageBox.Show((frame / DateTime.Now.Subtract(startTime).TotalSeconds).ToString() + " FPS");
			}
		}
	}

	public struct ColoredVertex
	{
		public Vector3 Position;
		public Vector4 Color;

		public ColoredVertex(Vector3 position, Vector4 color)
		{
			Position = position;
			Color = color;
		}
	}
}