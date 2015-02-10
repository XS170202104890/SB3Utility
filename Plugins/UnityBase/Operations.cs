﻿using System;
using System.Collections.Generic;
using System.IO;
using SlimDX;

using SB3Utility;

namespace UnityPlugin
{
	public static partial class Operations
	{
		public static Transform FindFrame(string name, Transform root)
		{
			Transform frame = root;
			if ((frame != null) && (frame.m_GameObject.instance.m_Name == name))
			{
				return frame;
			}

			for (int i = 0; i < root.Count; i++)
			{
				if ((frame = FindFrame(name, root[i])) != null)
				{
					return frame;
				}
			}

			return null;
		}

		public static int FindBoneIndex(List<PPtr<Transform>> boneList, Transform boneFrame)
		{
			for (int i = 0; i < boneList.Count; i++)
			{
				if (boneList[i].instance == boneFrame)
				{
					return i;
				}
			}
			return -1;
		}

		public static List<MeshRenderer> FindMeshes(Transform rootFrame, List<string> nameList)
		{
			List<MeshRenderer> meshList = new List<MeshRenderer>(nameList.Count);
			FindMeshFrames(rootFrame, meshList, nameList);
			return meshList;
		}

		static void FindMeshFrames(Transform frame, List<MeshRenderer> meshList, List<string> nameList)
		{
			MeshRenderer mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.SkinnedMeshRenderer);
			if (mesh == null)
			{
				mesh = frame.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshRenderer);
			}
			if ((mesh != null) && nameList.Contains(frame.m_GameObject.instance.m_Name))
			{
				meshList.Add(mesh);
			}

			for (int i = 0; i < frame.Count; i++)
			{
				FindMeshFrames(frame[i], meshList, nameList);
			}
		}

		public static Material FindMaterial(List<Material> matList, string name)
		{
			foreach (Material mat in matList)
			{
				if (mat.m_Name == name)
				{
					return mat;
				}
			}
			return null;
		}

		public static Texture2D FindTexture(List<Texture2D> texList, string name)
		{
			foreach (Texture2D tex in texList)
			{
				if (name.Contains(tex.m_Name))
				{
					return tex;
				}
			}
			return null;
		}

		public static void CopyUnknowns(Transform src, Transform dest)
		{
			dest.m_GameObject.instance.m_Layer = src.m_GameObject.instance.m_Layer;
			dest.m_GameObject.instance.m_Tag = src.m_GameObject.instance.m_Tag;
			dest.m_GameObject.instance.m_isActive = src.m_GameObject.instance.m_isActive;
		}

		public static void CreateUnknowns(Transform frame)
		{
			frame.m_GameObject.instance.m_isActive = true;
		}

		public static void CopyUnknowns(SkinnedMeshRenderer src, SkinnedMeshRenderer dest)
		{
			dest.m_Enabled = src.m_Enabled;
			dest.m_CastShadows = src.m_CastShadows;
			dest.m_ReceiveShadows = src.m_CastShadows;
			dest.m_LightmapIndex = src.m_LightmapIndex;
			dest.m_LightmapTilingOffset = src.m_LightmapTilingOffset;
			//m_SubsetIndices
			dest.m_StaticBatchRoot = src.m_StaticBatchRoot;
			dest.m_UseLightProbes = src.m_UseLightProbes;
			dest.m_LightProbeAnchor = src.m_LightProbeAnchor;
			dest.m_SortingLayerID = src.m_SortingLayerID;
			dest.m_SortingOrder = src.m_SortingOrder;
			dest.m_Quality = src.m_Quality;
			dest.m_UpdateWhenOffScreen = src.m_UpdateWhenOffScreen;
			//m_BlendShapeWeights
			dest.m_RootBone = src.m_RootBone;
			dest.m_AABB = src.m_AABB;
			dest.m_DirtyAABB = src.m_DirtyAABB;

			if (dest.m_Mesh.instance != null && src.m_Mesh.instance != null)
			{
				Mesh destMesh = dest.m_Mesh.instance;
				Mesh srcMesh = src.m_Mesh.instance;

				destMesh.m_Name = (string)srcMesh.m_Name.Clone();
				for (int i = 0; i < srcMesh.m_SubMeshes.Count && i < destMesh.m_SubMeshes.Count; i++)
				{
					destMesh.m_SubMeshes[i].topology = srcMesh.m_SubMeshes[i].topology;
				}
				//m_Shapes
				destMesh.m_IsReadable = srcMesh.m_IsReadable;
				destMesh.m_KeepVertices = srcMesh.m_KeepVertices;
				destMesh.m_KeepIndices = srcMesh.m_KeepIndices;
				destMesh.m_MeshUsageFlags = srcMesh.m_MeshUsageFlags;
			}
		}

		public static void CopyUnknowns(SubMesh src, SubMesh dst)
		{
			dst.topology = src.topology;
		}

		public static List<PPtr<Transform>> MergeBoneList(List<PPtr<Transform>> boneList1, List<PPtr<Transform>> boneList2, out int[] boneList2IdxMap)
		{
			boneList2IdxMap = new int[boneList2.Count];
			Dictionary<string, int> boneDic = new Dictionary<string, int>();
			List<PPtr<Transform>> mergedList = new List<PPtr<Transform>>(boneList1.Count + boneList2.Count);
			for (int i = 0; i < boneList1.Count; i++)
			{
				PPtr<Transform> transPtr = new PPtr<Transform>(boneList1[i].instance);
				boneDic.Add(transPtr.instance.m_GameObject.instance.m_Name, i);
				mergedList.Add(transPtr);
			}
			for (int i = 0; i < boneList2.Count; i++)
			{
				PPtr<Transform> transPtr = new PPtr<Transform>(boneList2[i].instance);
				int boneIdx;
				if (boneDic.TryGetValue(transPtr.instance.m_GameObject.instance.m_Name, out boneIdx))
				{
					mergedList[boneIdx] = transPtr;
				}
				else
				{
					boneIdx = mergedList.Count;
					mergedList.Add(transPtr);
					boneDic.Add(transPtr.instance.m_GameObject.instance.m_Name, boneIdx);
				}
				boneList2IdxMap[i] = boneIdx;
			}
			return mergedList;
		}

		public class vVertex
		{
			public Vector3 position;
			public Vector3 normal;
			public Vector4 tangent;
			public Vector2 uv;
			public int[] boneIndices;
			public float[] weights;
		}

		public class vFace
		{
			public ushort[] index;
		}

		public class vSubmesh
		{
			public List<vVertex> vertexList;
			public List<vFace> faceList;
			public List<PPtr<Material>> matList;

			public vSubmesh(Mesh mesh, int submeshIdx, bool faces, BinaryReader vertReader, BinaryReader indexReader)
			{
				SubMesh submesh = mesh.m_SubMeshes[submeshIdx];
				int numVertices = (int)submesh.vertexCount;
				List<BoneInfluence> weightList = mesh.m_Skin;
				vertexList = new List<vVertex>(numVertices);
				for (int str = 0; str < mesh.m_VertexData.m_Streams.Count; str++)
				{
					StreamInfo sInfo = mesh.m_VertexData.m_Streams[str];
					if (sInfo.channelMask == 0)
					{
						continue;
					}

					for (int j = 0; j < numVertices; j++)
					{
						vVertex vVertex;
						if (vertexList.Count < numVertices)
						{
							vVertex = new vVertex();
							vertexList.Add(vVertex);

							if (weightList.Count > 0)
							{
								vVertex.boneIndices = weightList[(int)submesh.firstVertex + j].boneIndex;
								vVertex.weights = weightList[(int)submesh.firstVertex + j].weight;
							}
						}
						else
						{
							vVertex = vertexList[j];
						}

						for (int chn = 0; chn < mesh.m_VertexData.m_Channels.Count; chn++)
						{
							ChannelInfo cInfo = mesh.m_VertexData.m_Channels[chn];
							if ((sInfo.channelMask & (1 << chn)) == 0)
							{
								continue;
							}

							vertReader.BaseStream.Position = sInfo.offset + (j + submesh.firstVertex) * sInfo.stride + cInfo.offset;
							switch (chn)
							{
							case 0:
								vVertex.position = new Vector3(vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle());
								break;
							case 1:
								vVertex.normal = new Vector3(vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle());
								break;
							case 3:
								vVertex.uv = new Vector2(vertReader.ReadSingle(), vertReader.ReadSingle());
								break;
							case 5:
								vVertex.tangent = new Vector4(vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle(), vertReader.ReadSingle());
								break;
							}
						}
					}
				}

				if (faces)
				{
					int numFaces = (int)(submesh.indexCount / 3);
					faceList = new List<vFace>(numFaces);
					indexReader.BaseStream.Position = submesh.firstByte;
					for (int i = 0; i < numFaces; i++)
					{
						vFace face = new vFace();
						face.index = new ushort[3] { (ushort)(indexReader.ReadUInt16() - submesh.firstVertex), (ushort)(indexReader.ReadUInt16() - submesh.firstVertex), (ushort)(indexReader.ReadUInt16() - submesh.firstVertex) };
						faceList.Add(face);
					}
				}
				matList = new List<PPtr<Material>>(1);
			}
		}

		public class vMesh
		{
			public List<vSubmesh> submeshes;
			protected MeshRenderer meshR;
			protected bool faces;

			public vMesh(MeshRenderer meshR, bool faces)
			{
				this.meshR = meshR;
				this.faces = faces;

				Mesh mesh;
				if (meshR is SkinnedMeshRenderer)
				{
					SkinnedMeshRenderer smr = (SkinnedMeshRenderer)meshR;
					mesh = smr.m_Mesh.instance;
				}
				else
				{
					MeshFilter filter = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
					mesh = filter.m_Mesh.instance;
				}
				submeshes = new List<vSubmesh>(mesh.m_SubMeshes.Count);
				using (BinaryReader vertReader = new BinaryReader(new MemoryStream(mesh.m_VertexData.m_DataSize)),
					indexReader = new BinaryReader(new MemoryStream(mesh.m_IndexBuffer)))
				{
					for (int i = 0; i < mesh.m_SubMeshes.Count; i++)
					{
						vSubmesh submesh = new vSubmesh(mesh, i, faces, vertReader, indexReader);
						if (i < meshR.m_Materials.Count)
						{
							submesh.matList.Add(meshR.m_Materials[i]);
							if (i == 0 && mesh.m_SubMeshes.Count < meshR.m_Materials.Count)
							{
								submesh.matList.AddRange(meshR.m_Materials.GetRange(mesh.m_SubMeshes.Count, meshR.m_Materials.Count - mesh.m_SubMeshes.Count));
							}
						}
						submeshes.Add(submesh);
					}
				}
			}

			public void Flush()
			{
				Mesh mesh;
				if (meshR is SkinnedMeshRenderer)
				{
					SkinnedMeshRenderer smr = (SkinnedMeshRenderer)meshR;
					mesh = smr.m_Mesh.instance;
				}
				else
				{
					MeshFilter filter = meshR.m_GameObject.instance.FindLinkedComponent(UnityClassID.MeshFilter);
					mesh = filter.m_Mesh.instance;
				}

				meshR.m_Materials.Clear();
				int totVerts = 0, totFaces = 0;
				for (int i = 0; i < submeshes.Count; i++)
				{
					vSubmesh submesh = submeshes[i];
					meshR.m_Materials.Insert(i, submesh.matList[0]);
					if (i == 0 && submesh.matList.Count > 1)
					{
						meshR.m_Materials.AddRange(submesh.matList.GetRange(1, submesh.matList.Count - 1));
					}

					totVerts += submesh.vertexList.Count;
					totFaces += submesh.faceList.Count;
				}
				if (mesh.m_VertexData.m_VertexCount != totVerts)
				{
					mesh.m_VertexData = new VertexData((uint)totVerts);
					if (mesh.m_Skin.Count > totVerts)
					{
						mesh.m_Skin.RemoveRange(0, mesh.m_Skin.Count - totVerts);
					}
					else
					{
						mesh.m_Skin.Capacity = totVerts;
						for (int i = mesh.m_Skin.Count; i < totVerts; i++)
						{
							BoneInfluence item = new BoneInfluence();
							mesh.m_Skin.Add(item);
						}
					}
				}
				if (mesh.m_IndexBuffer.Length != totFaces * 3 * sizeof(ushort))
				{
					mesh.m_IndexBuffer = new byte[totFaces * 3 * sizeof(ushort)];
				}
				using (BinaryWriter vertWriter = new BinaryWriter(new MemoryStream(mesh.m_VertexData.m_DataSize)),
					indexWriter = new BinaryWriter(new MemoryStream(mesh.m_IndexBuffer)))
				{
					int vertIndex = 0;
					for (int i = 0; i < submeshes.Count; i++)
					{
						SubMesh submesh;
						if (i < mesh.m_SubMeshes.Count)
						{
							submesh = mesh.m_SubMeshes[i];
						}
						else
						{
							submesh = new SubMesh();
							mesh.m_SubMeshes.Add(submesh);
						}
						submesh.indexCount = (uint)submeshes[i].faceList.Count * 3;
						submesh.vertexCount = (uint)submeshes[i].vertexList.Count;
						submesh.firstVertex = (uint)vertIndex;

						List<vVertex> vertexList = submeshes[i].vertexList;
						Vector3 min = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
						Vector3 max = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);
						bool skin = false;
						for (int str = 0; str < mesh.m_VertexData.m_Streams.Count; str++)
						{
							StreamInfo sInfo = mesh.m_VertexData.m_Streams[str];
							if (sInfo.channelMask == 0)
							{
								continue;
							}

							for (int j = 0; j < vertexList.Count; j++)
							{
								vVertex vert = vertexList[j];
								for (int chn = 0; chn < mesh.m_VertexData.m_Channels.Count; chn++)
								{
									if ((sInfo.channelMask & (1 << chn)) == 0)
									{
										continue;
									}

									ChannelInfo cInfo = mesh.m_VertexData.m_Channels[chn];
									vertWriter.BaseStream.Position = sInfo.offset + (j + submesh.firstVertex) * sInfo.stride + cInfo.offset;
									switch (chn)
									{
									case 0:
										vertWriter.Write(vert.position);
										min = Vector3.Minimize(min, vert.position);
										max = Vector3.Maximize(max, vert.position);
										break;
									case 1:
										vertWriter.Write(vert.normal);
										break;
									case 3:
										vertWriter.Write(vert.uv);
										break;
									case 5:
										vertWriter.Write(vert.tangent);
										break;
									}
								}

								if (!skin)
								{
									BoneInfluence item = mesh.m_Skin[(int)submesh.firstVertex + j];
									if (vert.boneIndices != null)
									{
										vert.boneIndices.CopyTo(item.boneIndex, 0);
										vert.weights.CopyTo(item.weight, 0);
									}
									else
									{
										item.boneIndex[0] = item.boneIndex[1] = item.boneIndex[2] = item.boneIndex[3] = -1;
									}
								}
							}
							skin = true;
						}
						vertIndex += (int)submesh.vertexCount;

						submesh.localAABB.m_Extend = (max - min) / 2;
						submesh.localAABB.m_Center = min + submesh.localAABB.m_Extend;
						mesh.m_LocalAABB.m_Extend = Vector3.Maximize(mesh.m_LocalAABB.m_Extend, submesh.localAABB.m_Extend);
						mesh.m_LocalAABB.m_Center += submesh.localAABB.m_Center;

						if (faces)
						{
							List<vFace> faceList = submeshes[i].faceList;
							submesh.firstByte = (uint)indexWriter.BaseStream.Position;
							for (int j = 0; j < faceList.Count; j++)
							{
								ushort[] vertexIndices = faceList[j].index;
								indexWriter.Write((ushort)(vertexIndices[0] + submesh.firstVertex));
								indexWriter.Write((ushort)(vertexIndices[1] + submesh.firstVertex));
								indexWriter.Write((ushort)(vertexIndices[2] + submesh.firstVertex));
							}
						}
					}
					mesh.m_LocalAABB.m_Center /= submeshes.Count;
					while (mesh.m_SubMeshes.Count > submeshes.Count)
					{
						mesh.m_SubMeshes.RemoveAt(mesh.m_SubMeshes.Count - 1);
					}
				}
			}
		}

		public static void CopyNormalsOrder(List<vVertex> src, List<vVertex> dest)
		{
			int len = (src.Count < dest.Count) ? src.Count : dest.Count;
			for (int i = 0; i < len; i++)
			{
				dest[i].normal = src[i].normal;
				dest[i].tangent = src[i].tangent;
			}
		}

		public static void CopyNormalsNear(List<vVertex> src, List<vVertex> dest)
		{
			for (int i = 0; i < dest.Count; i++)
			{
				var destVert = dest[i];
				var destPos = destVert.position;
				float minDistSq = Single.MaxValue;
				vVertex nearestVert = null;
				foreach (vVertex srcVert in src)
				{
					var srcPos = srcVert.position;
					float[] diff = new float[] { destPos[0] - srcPos[0], destPos[1] - srcPos[1], destPos[2] - srcPos[2] };
					float distSq = (diff[0] * diff[0]) + (diff[1] * diff[1]) + (diff[2] * diff[2]);
					if (distSq < minDistSq)
					{
						minDistSq = distSq;
						nearestVert = srcVert;
					}
				}

				destVert.normal = nearestVert.normal;
				destVert.tangent = nearestVert.tangent;
			}
		}

		public static void CopyBonesOrder(List<vVertex> src, List<vVertex> dest)
		{
			int len = (src.Count < dest.Count) ? src.Count : dest.Count;
			for (int i = 0; i < len; i++)
			{
				dest[i].boneIndices = (int[])src[i].boneIndices.Clone();
				dest[i].weights = (float[])src[i].weights.Clone();
			}
		}

		public static void CopyBonesNear(List<vVertex> src, List<vVertex> dest)
		{
			for (int i = 0; i < dest.Count; i++)
			{
				var destVert = dest[i];
				var destPos = destVert.position;
				float minDistSq = Single.MaxValue;
				vVertex nearestVert = null;
				foreach (vVertex srcVert in src)
				{
					var srcPos = srcVert.position;
					float[] diff = new float[] { destPos[0] - srcPos[0], destPos[1] - srcPos[1], destPos[2] - srcPos[2] };
					float distSq = (diff[0] * diff[0]) + (diff[1] * diff[1]) + (diff[2] * diff[2]);
					if (distSq < minDistSq)
					{
						minDistSq = distSq;
						nearestVert = srcVert;
					}
				}

				destVert.boneIndices = (int[])nearestVert.boneIndices.Clone();
				destVert.weights = (float[])nearestVert.weights.Clone();
			}
		}

		private class VertexRef
		{
			public vVertex vert;
			public Vector3 norm;
		}

		private class VertexRefComparerX : IComparer<VertexRef>
		{
			public int Compare(VertexRef x, VertexRef y)
			{
				return System.Math.Sign(x.vert.position[0] - y.vert.position[0]);
			}
		}

		private class VertexRefComparerY : IComparer<VertexRef>
		{
			public int Compare(VertexRef x, VertexRef y)
			{
				return System.Math.Sign(x.vert.position[1] - y.vert.position[1]);
			}
		}

		private class VertexRefComparerZ : IComparer<VertexRef>
		{
			public int Compare(VertexRef x, VertexRef y)
			{
				return System.Math.Sign(x.vert.position[2] - y.vert.position[2]);
			}
		}

		public static void CalculateNormals(List<vSubmesh> submeshes, float threshold)
		{
			var pairList = new List<Tuple<List<vFace>, List<vVertex>>>(submeshes.Count);
			for (int i = 0; i < submeshes.Count; i++)
			{
				pairList.Add(new Tuple<List<vFace>, List<vVertex>>(submeshes[i].faceList, submeshes[i].vertexList));
			}
			CalculateNormals(pairList, threshold);
		}

		public static void CalculateNormals(List<Tuple<List<vFace>, List<vVertex>>> pairList, float threshold)
		{
			if (threshold < 0)
			{
				VertexRef[][] vertRefArray = new VertexRef[pairList.Count][];
				for (int i = 0; i < pairList.Count; i++)
				{
					List<vVertex> vertList = pairList[i].Item2;
					vertRefArray[i] = new VertexRef[vertList.Count];
					for (int j = 0; j < vertList.Count; j++)
					{
						vVertex vert = vertList[j];
						VertexRef vertRef = new VertexRef();
						vertRef.vert = vert;
						vertRef.norm = new Vector3();
						vertRefArray[i][j] = vertRef;
					}
				}

				for (int i = 0; i < pairList.Count; i++)
				{
					List<vFace> faceList = pairList[i].Item1;
					for (int j = 0; j < faceList.Count; j++)
					{
						vFace face = faceList[j];
						Vector3 v1 = vertRefArray[i][face.index[1]].vert.position - vertRefArray[i][face.index[2]].vert.position;
						Vector3 v2 = vertRefArray[i][face.index[0]].vert.position - vertRefArray[i][face.index[2]].vert.position;
						Vector3 norm = Vector3.Cross(v2, v1);
						norm.Normalize();
						for (int k = 0; k < face.index.Length; k++)
						{
							vertRefArray[i][face.index[k]].norm += norm;
						}
					}
				}

				for (int i = 0; i < vertRefArray.Length; i++)
				{
					for (int j = 0; j < vertRefArray[i].Length; j++)
					{
						Vector3 norm = vertRefArray[i][j].norm;
						norm.Normalize();
						vertRefArray[i][j].vert.normal = norm;
					}
				}
			}
			else
			{
				int vertCount = 0;
				for (int i = 0; i < pairList.Count; i++)
				{
					vertCount += pairList[i].Item2.Count;
				}

				VertexRefComparerX vertRefComparerX = new VertexRefComparerX();
				List<VertexRef> vertRefListX = new List<VertexRef>(vertCount);
				VertexRef[][] vertRefArray = new VertexRef[pairList.Count][];
				for (int i = 0; i < pairList.Count; i++)
				{
					var vertList = pairList[i].Item2;
					vertRefArray[i] = new VertexRef[vertList.Count];
					for (int j = 0; j < vertList.Count; j++)
					{
						vVertex vert = vertList[j];
						VertexRef vertRef = new VertexRef();
						vertRef.vert = vert;
						vertRef.norm = new Vector3();
						vertRefArray[i][j] = vertRef;
						vertRefListX.Add(vertRef);
					}
				}
				vertRefListX.Sort(vertRefComparerX);

				for (int i = 0; i < pairList.Count; i++)
				{
					var faceList = pairList[i].Item1;
					for (int j = 0; j < faceList.Count; j++)
					{
						vFace face = faceList[j];
						Vector3 v1 = vertRefArray[i][face.index[1]].vert.position - vertRefArray[i][face.index[2]].vert.position;
						Vector3 v2 = vertRefArray[i][face.index[0]].vert.position - vertRefArray[i][face.index[2]].vert.position;
						Vector3 norm = Vector3.Cross(v2, v1);
						norm.Normalize();
						for (int k = 0; k < face.index.Length; k++)
						{
							vertRefArray[i][face.index[k]].norm += norm;
						}
					}
				}

				float squaredThreshold = threshold * threshold;
				while (vertRefListX.Count > 0)
				{
					VertexRef vertRef = vertRefListX[vertRefListX.Count - 1];
					List<VertexRef> dupList = new List<VertexRef>();
					List<VertexRef> dupListX = GetAxisDups(vertRef, vertRefListX, 0, threshold, null);
					foreach (VertexRef dupRef in dupListX)
					{
						if (((vertRef.vert.position.Y - dupRef.vert.position.Y) <= threshold) &&
							((vertRef.vert.position.Z - dupRef.vert.position.Z) <= threshold) &&
							((vertRef.vert.position - dupRef.vert.position).LengthSquared() <= squaredThreshold))
						{
							dupList.Add(dupRef);
						}
					}
					vertRefListX.RemoveAt(vertRefListX.Count - 1);

					Vector3 norm = vertRef.norm;
					foreach (VertexRef dupRef in dupList)
					{
						norm += dupRef.norm;
						vertRefListX.Remove(dupRef);
					}
					norm.Normalize();

					vertRef.vert.normal = norm;
					foreach (VertexRef dupRef in dupList)
					{
						dupRef.vert.normal = norm;
						vertRefListX.Remove(dupRef);
					}
				}
			}
		}

		private static List<VertexRef> GetAxisDups(VertexRef vertRef, List<VertexRef> compareList, int axis, float threshold, IComparer<VertexRef> binaryComparer)
		{
			List<VertexRef> dupList = new List<VertexRef>();
			int startIdx;
			if (binaryComparer == null)
			{
				startIdx = compareList.IndexOf(vertRef);
				if (startIdx < 0)
				{
					throw new Exception("Vertex wasn't found in the compare list");
				}
			}
			else
			{
				startIdx = compareList.BinarySearch(vertRef, binaryComparer);
				if (startIdx < 0)
				{
					startIdx = ~startIdx;
				}
				if (startIdx < compareList.Count)
				{
					VertexRef compRef = compareList[startIdx];
					if (System.Math.Abs(vertRef.vert.position[axis] - compRef.vert.position[axis]) <= threshold)
					{
						dupList.Add(compRef);
					}
				}
			}

			for (int i = startIdx + 1; i < compareList.Count; i++)
			{
				VertexRef compRef = compareList[i];
				if ((System.Math.Abs(vertRef.vert.position[axis] - compRef.vert.position[axis]) <= threshold))
				{
					dupList.Add(compRef);
				}
				else
				{
					break;
				}
			}
			for (int i = startIdx - 1; i >= 0; i--)
			{
				VertexRef compRef = compareList[i];
				if ((System.Math.Abs(vertRef.vert.position[axis] - compRef.vert.position[axis]) <= threshold))
				{
					dupList.Add(compRef);
				}
				else
				{
					break;
				}
			}
			return dupList;
		}

		public static void CalculateTangents(List<vSubmesh> submeshes)
		{
			foreach (vSubmesh submesh in submeshes)
			{
				CalculateTangents(submesh.vertexList, submesh.faceList);
			}
		}

		// http://www.terathon.com/code/tangent.html
		public static void CalculateTangents(List<vVertex> vertList, List<vFace> triangle)
		{
			int vertexCount = vertList.Count;
			Vector3[] tan1 = new Vector3[vertexCount];
			Vector3[] tan2 = new Vector3[vertexCount];

			int triangleCount = triangle.Count;
			for (int a = 0; a < triangleCount; a++)
			{
				int i1 = triangle[a].index[0];
				int i2 = triangle[a].index[1];
				int i3 = triangle[a].index[2];

				vVertex vertex1 = vertList[i1];
				vVertex vertex2 = vertList[i2];
				vVertex vertex3 = vertList[i3];

				Vector3 v1 = vertex1.position;
				Vector3 v2 = vertex2.position;
				Vector3 v3 = vertex3.position;

				Vector2 w1 = vertex1.uv;
				Vector2 w2 = vertex2.uv;
				Vector2 w3 = vertex3.uv;

				float x1 = v2.X - v1.X;
				float x2 = v3.X - v1.X;
				float y1 = v2.Y - v1.Y;
				float y2 = v3.Y - v1.Y;
				float z1 = v2.Z - v1.Z;
				float z2 = v3.Z - v1.Z;

				float s1 = w2.X - w1.X;
				float s2 = w3.X - w1.X;
				float t1 = w2.Y - w1.Y;
				float t2 = w3.Y - w1.Y;

				float r = 1.0F / (s1 * t2 - s2 * t1);
				Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r,
						(t2 * z1 - t1 * z2) * r);
				Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r,
						(s1 * z2 - s2 * z1) * r);

				tan1[i1] += sdir;
				tan1[i2] += sdir;
				tan1[i3] += sdir;

				tan2[i1] += tdir;
				tan2[i2] += tdir;
				tan2[i3] += tdir;
			}

			for (int a = 0; a < vertexCount; a++)
			{
				Vector3 n = vertList[a].normal;
				Vector3 t = tan1[a];

				// Gram-Schmidt orthogonalize
				vertList[a].tangent = new Vector4(Vector3.Normalize(t - n * Vector3.Dot(n, t)),

				// Calculate handedness
				(Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0F) ? -1.0F : 1.0F);
			}
		}
	}
}