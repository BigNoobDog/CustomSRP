using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;

namespace MSRP
{
	
	public class Lighting
	{
		const string bufferName = "Lighting";

		const int maxDirLightCount = 4, maxOtherLightCount = 64;

		static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

		static int
			dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
			dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
			dirLightDirectionsAndMasksId =
				Shader.PropertyToID("_DirectionalLightDirectionsAndMasks"),
			dirLightShadowDataId =
				Shader.PropertyToID("_DirectionalLightShadowData");

		static Vector4[]
			dirLightColors = new Vector4[maxDirLightCount],
			dirLightDirectionsAndMasks = new Vector4[maxDirLightCount],
			dirLightShadowData = new Vector4[maxDirLightCount];

		private static int
			otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
			otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
			otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
			otherLightDirectionsAndMasksId =
				Shader.PropertyToID("_OtherLightDirectionsAndMasks"),
			otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
			areaLightXDirectionsId = Shader.PropertyToID("_AreaLightXDirections"),
			areaLightYDirectionsId = Shader.PropertyToID("_AreaLightYDirections"),
			otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData"),
			areaLightSourceTextureId =
				Shader.PropertyToID("_AreaLightSourceTexture");

		static Vector4[]
			otherLightColors = new Vector4[maxOtherLightCount],
			otherLightPositions = new Vector4[maxOtherLightCount],
			otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount],
			otherLightSpotAngles = new Vector4[maxOtherLightCount],
			areaLightXDirections = new Vector4[maxOtherLightCount],
			areaLightYDirections = new Vector4[maxOtherLightCount],
			otherLightShadowData = new Vector4[maxOtherLightCount];

		CommandBuffer buffer = new CommandBuffer
		{
			name = bufferName
		};

		CullingResults cullingResults;

		Shadows shadows = new Shadows();

		private PipelineLightSetting pipelineLightSetting;

		private bool areaLightEnable;

		private ScriptableRenderContext context;
		
		static LightSettings defaultLightSettings = new LightSettings();

		private bool useLightsPerObject;

		private int renderingLayerMask;
		
		public void Setup(
			ScriptableRenderContext context, CullingResults cullingResults,
			ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask, PipelineLightSetting pipelineLightSetting
		)
		{
			this.cullingResults = cullingResults;
			this.context = context;
			this.pipelineLightSetting = pipelineLightSetting;
			this.renderingLayerMask = renderingLayerMask;
			this.useLightsPerObject = useLightsPerObject;
			buffer.BeginSample(bufferName);
			shadows.Setup(context, cullingResults, shadowSettings);
			SetupLights(useLightsPerObject, renderingLayerMask);
			shadows.Render();
			buffer.EndSample(bufferName);
			context.ExecuteCommandBuffer(buffer);
			buffer.Clear();
		}

		void ExecuteBuffer()
		{
			context.ExecuteCommandBuffer(buffer);
			buffer.Clear();
		}

		public void Cleanup()
		{
			shadows.Cleanup();
		}

		void SetupLights(bool useLightsPerObject, int renderingLayerMask)
		{
			NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
			NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
			int dirLightCount = 0, otherLightCount = 0;
			int i;
			areaLightEnable = false;
			for (i = 0; i < visibleLights.Length; i++)
			{
				int newIndex = -1;
				VisibleLight visibleLight = visibleLights[i];
				Light light = visibleLight.light;
				var crpCamera = light.GetComponent<CustomRenderPipelineLight>();
				LightSettings lightSettings =
					crpCamera ? crpCamera.Settings : defaultLightSettings;
				if ((light.renderingLayerMask & renderingLayerMask) != 0)
				{
					switch (visibleLight.lightType)
					{
						case LightType.Directional:
							if (dirLightCount < maxDirLightCount)
							{
								SetupDirectionalLight(
									dirLightCount++, i, ref visibleLight, light
								);
							}

							break;
						case LightType.Point:
							if (otherLightCount < maxOtherLightCount)
							{
								newIndex = otherLightCount;
								SetupPointLight(
									otherLightCount++, i, ref visibleLight, light
								);
							}

							break;
						case LightType.Spot:
							if (otherLightCount < maxOtherLightCount)
							{
								newIndex = otherLightCount;
								if (lightSettings.areaLightTpye != AreaLightTpye.None)
								{
									SetupAreaLight(otherLightCount++, i, ref visibleLight, light, lightSettings);
									areaLightEnable = true;
								}
								else
								{
									SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
								}
							}

							break;
						case LightType.Area:
							if (otherLightCount < maxOtherLightCount)
							{
								newIndex = otherLightCount;
								SetupAreaLight(otherLightCount++, i, ref visibleLight, light, lightSettings);
								areaLightEnable = true;
							}
							break;
					}
				}

				if (useLightsPerObject)
				{
					indexMap[i] = newIndex;
				}
			}

			if (useLightsPerObject)
			{
				for (; i < indexMap.Length; i++)
				{
					indexMap[i] = -1;
				}

				cullingResults.SetLightIndexMap(indexMap);
				indexMap.Dispose();
				Shader.EnableKeyword(lightsPerObjectKeyword);
			}
			else
			{
				Shader.DisableKeyword(lightsPerObjectKeyword);
			}

			buffer.SetGlobalInt(dirLightCountId, dirLightCount);
			if (dirLightCount > 0)
			{
				buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
				buffer.SetGlobalVectorArray(
					dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks
				);
				buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
			}

			buffer.SetGlobalInt(otherLightCountId, otherLightCount);
			if (otherLightCount > 0)
			{
				buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
				buffer.SetGlobalVectorArray(
					otherLightPositionsId, otherLightPositions
				);
				buffer.SetGlobalVectorArray(
					otherLightDirectionsAndMasksId, otherLightDirectionsAndMasks
				);
				buffer.SetGlobalVectorArray(
					otherLightSpotAnglesId, otherLightSpotAngles
				);
				buffer.SetGlobalVectorArray(
					areaLightYDirectionsId, areaLightYDirections
				);
				buffer.SetGlobalVectorArray(
					areaLightXDirectionsId, areaLightXDirections
				);
				// buffer.SetGlobalTexture(
				// 	areaLightSourceTextureId, areaLightSourceTexture
				// );
				buffer.SetGlobalVectorArray(
					otherLightShadowDataId, otherLightShadowData
				);
			}
			
			if (areaLightEnable)
			{
				GenAreaLightLut();
			}
		}

		void SetupDirectionalLight(
			int index, int visibleIndex, ref VisibleLight visibleLight, Light light
		)
		{
			dirLightColors[index] = visibleLight.finalColor;
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			dirLightDirectionsAndMasks[index] = dirAndMask;
			dirLightShadowData[index] =
				shadows.ReserveDirectionalShadows(light, visibleIndex);
		}

		void SetupPointLight(
			int index, int visibleIndex, ref VisibleLight visibleLight, Light light
		)
		{
			otherLightColors[index] = visibleLight.finalColor;
			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w =
				1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
			otherLightPositions[index] = position;
			otherLightSpotAngles[index] = new Vector4(0f, 1f);
			Vector4 dirAndmask = Vector4.zero;
			dirAndmask.w = light.renderingLayerMask.ReinterpretAsFloat();
			otherLightDirectionsAndMasks[index] = dirAndmask;
			//Light light = visibleLight.light;
			otherLightShadowData[index] =
				shadows.ReserveAddLightShadows(light, visibleIndex);
		}

		void SetupSpotLight(
			int index, int visibleIndex, ref VisibleLight visibleLight, Light light
		)
		{
			otherLightColors[index] = visibleLight.finalColor;
			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w =
				1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
			otherLightPositions[index] = position;
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			otherLightDirectionsAndMasks[index] = dirAndMask;

			float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
			float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
			float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
			otherLightSpotAngles[index] = new Vector4(
				angleRangeInv, -outerCos * angleRangeInv
			);
			otherLightShadowData[index] =
				shadows.ReserveAddLightShadows(light, visibleIndex);
		}

		void GenAreaLightLut()
		{
			//Gen LUT
			// int lutWidth = 32, lutHeight = 16;
			// buffer.GetTemporaryRT(areaLightLUTId, lutWidth, lutHeight, 0, FilterMode.Point,
			// 	GraphicsFormat.R16G16B16A16_SFloat, 1, true);
			//
			// int kernel = pipelineLightSetting.areaLightLutCS.FindKernel("GenLut");
			// buffer.SetComputeTextureParam(pipelineLightSetting.areaLightLutCS, kernel, areaLightLUTId, areaLightLUTId);
			// buffer.DispatchCompute(pipelineLightSetting.areaLightLutCS, kernel, lutWidth / 8, lutHeight / 8, 1);
			// buffer.SetGlobalTexture(areaLightLUTId, areaLightLUTId);
			// ExecuteBuffer();
			
			if (LTCAreaLight.instance.m_LtcData == null)
				LTCAreaLight.instance.BuildLUT();
			LTCAreaLight.instance.Bind(buffer);
			
			
			// Shader.SetGlobalTexture(areaLightLUT_1_Id, pipelineLightSetting.matLUT);
			// Shader.SetGlobalTexture(areaLightLUT_2_Id, pipelineLightSetting.ampLUT);
		}

		void SetupAreaLight(
			int index, int visibleIndex, ref VisibleLight visibleLight, Light light, LightSettings lightSettings
		)
		{
			otherLightColors[index] = visibleLight.finalColor;
			Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
			position.w =
				1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
			otherLightPositions[index] = position;
			Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
			dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
			otherLightDirectionsAndMasks[index] = dirAndMask;

			float width = lightSettings.RectWidth;
			float height = lightSettings.RectHeight;
			
			Vector4 yAxis = visibleLight.localToWorldMatrix.GetColumn(1);
			Vector4 xAxis = visibleLight.localToWorldMatrix.GetColumn(0);
			
			otherLightSpotAngles[index] = new Vector4(
				0, 1, width, height
			);
			areaLightXDirections[index] = xAxis;
			areaLightXDirections[index].w = 1;
			areaLightYDirections[index] = yAxis;
			otherLightShadowData[index] =
				shadows.ReserveAddLightShadows(light, visibleIndex);
			// areaLightSourceTexture[index] = lightSettings.SourceTexture;
		}

		public void DrawAreaLight()
		{
			NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
			NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
			int dirLightCount = 0, otherLightCount = 0;
			int i;
			areaLightEnable = false;
			for (i = 0; i < visibleLights.Length; i++)
			{
				int newIndex = -1;
				VisibleLight visibleLight = visibleLights[i];
				Light light = visibleLight.light;
				var crpCamera = light.GetComponent<CustomRenderPipelineLight>();
				LightSettings lightSettings =
					crpCamera ? crpCamera.Settings : defaultLightSettings;
				if ((light.renderingLayerMask & renderingLayerMask) != 0)
				{
					switch (visibleLight.lightType)
					{
						case LightType.Spot:
							if (dirLightCount < maxDirLightCount && lightSettings.areaLightTpye != AreaLightTpye.None)
							{
								Mesh quad = GenQuad(new Vector2(lightSettings.RectWidth, lightSettings.RectHeight));
								Material mat = MonoBehaviour.Instantiate<Material>(pipelineLightSetting.AreaLightMat);
								// mat.SetTexture("_BaseMap", lightSettings.SourceTexture);
								mat.SetColor("_BaseColor", light.color * light.intensity / 3.1415926f);
								buffer.DrawMesh(quad, light.transform.localToWorldMatrix, mat, 0, 0);
								ExecuteBuffer();
							}
							break;;
					}
				}
			}
		}

		Mesh GenQuad(Vector2 rect)
		{
			Mesh mesh = new Mesh();

			Vector3[] vertices = new Vector3[4]
			{
				new Vector3(-rect.x, -rect.y, 0),
				new Vector3(-rect.x, rect.y, 0),
				new Vector3(rect.x, -rect.y, 0),
				new Vector3(rect.x, rect.y, 0)
			};
			mesh.vertices = vertices;

			int[] tris = new int[6]
			{
				// lower left triangle
				0, 2, 1,
				// upper right triangle
				2, 3, 1
			};
			mesh.triangles = tris;

			Vector3[] normals = new Vector3[4]
			{
				-Vector3.forward,
				-Vector3.forward,
				-Vector3.forward,
				-Vector3.forward
			};
			mesh.normals = normals;

			Vector2[] uv = new Vector2[4]
			{
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0, 1),
				new Vector2(1, 1)
			};
			mesh.uv = uv;
			return mesh;
		}
	}
}