﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using SB3Utility;

namespace UnityPlugin
{
	public class AssetCabinet : NeedsSourceStreamForWriting
	{
		public string Name { get; protected set; }
		public int Unknown1 { get; protected set; }
		public int HeaderLength { get; protected set; }
		public int ContentLength { get; protected set; }
		public byte[] Alignment1 { get; protected set; }
		public int UsedLength { get; protected set; }
		public int ContentLengthCopy { get; protected set; }
		public int Format { get; protected set; }
		public int DataPosition { get; protected set; }
		public int Unknown6 { get; protected set; }
		public string Version { get; protected set; }
		public int Unknown7 { get; protected set; }

		public class TypeDefinitionString
		{
			public string type;
			public string identifier;
			public int[] flags;
			public TypeDefinitionString[] children;
		}
		public class TypeDefinition
		{
			public int typeId;
			public TypeDefinitionString definitions;
		}
		public TypeDefinition[] Types { get; protected set; }
		public int Unknown8 { get; protected set; }

		public List<Component> Components { get; protected set; }

		public class Reference
		{
			public Guid guid;
			public int type;
			public String filePath;
			public String assetPath;
		}
		public Reference[] References { get; protected set; }

		public Stream SourceStream { get; set; }
		public UnityParser Parser { get; set; }
		public bool loadingReferencials { get; set; }
		public List<NotLoaded> RemovedList { get; set; }
		HashSet<string> reported;

		public AssetCabinet(Stream stream, UnityParser parser)
		{
			Parser = parser;
			BinaryReader reader = new BinaryReader(stream);
			Unknown1 = reader.ReadInt32BE();
			Name = reader.ReadName0();
			HeaderLength = reader.ReadInt32BE();
			ContentLength = reader.ReadInt32BE();
			Alignment1 = reader.ReadBytes(3);

			// Address 0x70 seems to be the beginning of a structure
			UsedLength = reader.ReadInt32BE();
			ContentLengthCopy = reader.ReadInt32BE();
			Format = reader.ReadInt32BE();
			DataPosition = reader.ReadInt32BE();
			Unknown6 = reader.ReadInt32BE();
//			Console.WriteLine(" 1=" + Unknown1 + " 2=x" + Unknown2[0].ToString("X2") + Unknown2[1].ToString("X2") + Unknown2[2].ToString("X2") + " UL=x"+ UsedLength.ToString("X6") + " F=" + Format +" DP=x"+DataPosition.ToString("X6") + " 6=" + Unknown6);
			Version = reader.ReadName0();

			Unknown7 = reader.ReadInt32();

			int numTypes = reader.ReadInt32();
			Types = new TypeDefinition[numTypes];
			for (int i = 0; i < numTypes; i++)
			{
				Types[i] = new TypeDefinition();
				Types[i].typeId = reader.ReadInt32();
				Types[i].definitions = new TypeDefinitionString();
				ReadType(reader, Types[i].definitions);
			}

			Unknown8 = reader.ReadInt32();

			int numComponents = reader.ReadInt32();
			Components = new List<Component>(numComponents);
			for (int i = 0; i < numComponents; i++)
			{
				NotLoaded comp = new NotLoaded();
				comp.pathID = reader.ReadInt32();
				comp.offset = (uint)0x70 + (uint)DataPosition + reader.ReadUInt32();
				comp.size = reader.ReadUInt32();
				comp.classID1 = (UnityClassID)reader.ReadInt32();
				comp.classID2 = (UnityClassID)reader.ReadInt32();
				Components.Add(comp);
			}

			int numRefs = reader.ReadInt32();
			References = new Reference[numRefs];
			for (int i = 0; i < numRefs; i++)
			{
				References[i] = new Reference();
				References[i].guid = new Guid(reader.ReadBytes(16));
				References[i].type = reader.ReadInt32();
				References[i].filePath = reader.ReadName0();
				References[i].assetPath = reader.ReadName0();
			}
			if (stream.Position != UsedLength + 0x83)
			{
				Report.ReportLog("Unexpected Length");
			}
			long padding = (stream.Position + 16) & ~(long)15;
			if (padding != 0x70 + DataPosition)
			{
				Report.ReportLog("Unexpected DataPosition");
			}

			RemovedList = new List<NotLoaded>();
			loadingReferencials = false;
			reported = new HashSet<string>();
		}

		private void ReadType(BinaryReader reader, TypeDefinitionString tds)
		{
			tds.type = reader.ReadName0();
			tds.identifier = reader.ReadName0();
			tds.flags = new int[5];
			for (int i = 0; i < 5; i++)
			{
				tds.flags[i] = reader.ReadInt32();
			}
			int numChildren = reader.ReadInt32();
			tds.children = new TypeDefinitionString[numChildren];
			for (int i = 0; i < numChildren; i++)
			{
				tds.children[i] = new TypeDefinitionString();
				ReadType(reader, tds.children[i]);
			}
		}

		public void WriteTo(Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			long beginPos = stream.Position;
			stream.Position += 4 + Name.Length + 1 + 4 + 4 + Alignment1.Length + 4 + 4 + 4 + 4;

			writer.WriteInt32BE(Unknown6);
			writer.WriteName0(Version);

			writer.Write(Unknown7);

			writer.Write(Types.Length);
			for (int i = 0; i < Types.Length; i++)
			{
				writer.Write(Types[i].typeId);
				WriteType(writer, Types[i].definitions);
			}

			writer.Write(Unknown8);

			writer.Write(Components.Count);
			long assetMetaPosition = stream.Position;
			stream.Position += Components.Count * 5 * sizeof(int);

			writer.Write(References.Length);
			for (int i = 0; i < References.Length; i++)
			{
				writer.Write(References[i].guid.ToByteArray());
				writer.Write(References[i].type);
				writer.WriteName0(References[i].filePath);
				writer.WriteName0(References[i].assetPath);
			}
			UsedLength = (int)stream.Position - 0x83;
			stream.Position = (stream.Position + 16) & ~(long)15;
			DataPosition = (int)stream.Position - 0x70;

			uint[] offsets = new uint[Components.Count];
			uint[] sizes = new uint[Components.Count];
			byte[] align = new byte[3];
			for (int i = 0; i < Components.Count; i++)
			{
				offsets[i] = (uint)stream.Position;
				Component comp = Components[i];
				if (comp is NeedsSourceStreamForWriting)
				{
					((NeedsSourceStreamForWriting)comp).SourceStream = SourceStream;
				}
				comp.WriteTo(stream);
				sizes[i] = (uint)stream.Position - offsets[i];
				int rest = 4 - (int)(stream.Position & 3);
				if (rest < 4 && i < Components.Count - 1)
				{
					writer.Write(align, 0, rest);
				}
				Parser.worker.ReportProgress(50 + i * 49 / Components.Count);
			}
			ContentLength = ContentLengthCopy = (int)stream.Position - 0x70;

			stream.Position = beginPos;
			writer.WriteInt32BE(Unknown1);
			writer.WriteName0(Name);
			writer.WriteInt32BE(HeaderLength);
			writer.WriteInt32BE(ContentLength);
			writer.Write(Alignment1);

			writer.WriteInt32BE(UsedLength);
			writer.WriteInt32BE(ContentLengthCopy);
			writer.WriteInt32BE(Format);
			writer.WriteInt32BE(DataPosition);

			stream.Position = assetMetaPosition;
			for (int i = 0; i < Components.Count; i++)
			{
				Component comp = Components[i];
				writer.Write(comp.pathID);
				writer.Write(offsets[i] - (uint)DataPosition - (uint)0x70);
				writer.Write(sizes[i]);
				writer.Write((int)comp.classID1);
				writer.Write((int)comp.classID2);
				if (comp is NotLoaded)
				{
					((NotLoaded)comp).offset = offsets[i];
				}
			}
		}

		private void WriteType(BinaryWriter writer, TypeDefinitionString tds)
		{
			writer.WriteName0(tds.type);
			writer.WriteName0(tds.identifier);
			for (int i = 0; i < tds.flags.Length; i++)
			{
				writer.Write(tds.flags[i]);
			}
			writer.Write(tds.children.Length);
			for (int i = 0; i < tds.children.Length; i++)
			{
				WriteType(writer, tds.children[i]);
			}
		}

		public dynamic FindComponentIndex(int pathID)
		{
			for (int i = 0; i < Components.Count; i++)
			{
				Component comp = Components[i];
				if (comp.pathID == pathID)
				{
					return i;
				}
			}
			return -1;
		}

		public dynamic FindComponent(int pathID, out int index)
		{
			if (pathID == 0)
			{
				index = -1;
				return null;
			}
			index = pathID - 1 < Components.Count ? pathID - 1 : Components.Count - 1;
			Component asset = Components[index];
			if (asset.pathID == pathID)
			{
				return asset;
			}
			try
			{
				int i;
				for (i = index + 1; asset.pathID < pathID; i++)
				{
					asset = Components[i];
				}
				if (asset.pathID == pathID)
				{
					index = i - 1;
					return asset;
				}
				for (i = index - 1; asset.pathID == 0 || asset.pathID > pathID; i--)
				{
					asset = Components[i];
				}
				if (asset.pathID == pathID)
				{
					index = i + 1;
					return asset;
				}
			}
			catch { }

			index = -1;
			return null;
		}

		public dynamic FindComponent(int pathID)
		{
			int index_not_required;
			return FindComponent(pathID, out index_not_required);
		}

		public dynamic LoadComponent(int pathID)
		{
			if (pathID == 0)
			{
				return null;
			}

			int index;
			Component subfile = FindComponent(pathID, out index);
			NotLoaded comp = subfile as NotLoaded;
			if (comp == null)
			{
				return subfile;
			}

			using (Stream stream = File.OpenRead(Parser.FilePath))
			{
				//stream.Position = comp.offset;
				//using (PartialStream ps = new PartialStream(stream, comp.size))
				{
					Component asset = LoadComponent(/*ps*/stream, index, comp);
					/*if (!loadingReferencials &&
						comp.offset + comp.size != stream.Position &&
						(comp.classID1 != UnityClassID.SkinnedMeshRenderer ||
							comp.offset + comp.size - 3 != stream.Position))
					{
						Console.WriteLine(comp.classID1 + " ctr read bad length" + (stream.Position - comp.offset - comp.size));
					}*/
					return asset != null ? asset : subfile;
				}
			}
		}

		public dynamic LoadComponent(Stream stream, NotLoaded comp)
		{
			return LoadComponent(stream, Components.IndexOf(comp), comp);
		}

		public dynamic LoadComponent(Stream stream, int index, NotLoaded comp)
		{
			stream.Position = comp.offset;
			switch (comp.classID1)
			{
			case UnityClassID.AnimationClip:
				{
					AnimationClip animationClip = new AnimationClip(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, animationClip, comp);
					animationClip.LoadFrom(stream);
					return animationClip;
				}
			case UnityClassID.Animator:
				{
					Animator animator = new Animator(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, animator, comp);
					animator.LoadFrom(stream);
					return animator;
				}
			case UnityClassID.AnimatorController:
				throw new Exception("Unhandled " + comp.classID1 + " (would corrupt output file)");
			case UnityClassID.AssetBundle:
				{
					AssetBundle assetBundle = new AssetBundle(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, assetBundle, comp);
					assetBundle.LoadFrom(stream);
					return assetBundle;
				}
			case UnityClassID.AudioClip:
				{
					if (loadingReferencials)
					{
						return comp;
					}
					AudioClip ac = new AudioClip(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, ac, comp);
					ac.LoadFrom(stream);
					return ac;
				}
			case UnityClassID.Avatar:
				{
					if (loadingReferencials)
					{
						return comp;
					}
					Avatar avatar = new Avatar(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, avatar, comp);
					avatar.LoadFrom(stream);
					return avatar;
				}
			case UnityClassID.Cubemap:
				{
					Cubemap cubemap = new Cubemap(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, cubemap, comp);
					cubemap.LoadFrom(stream);
					return cubemap;
				}
			case UnityClassID.EllipsoidParticleEmitter:
				{
					EllipsoidParticleEmitter ellipsoid = new EllipsoidParticleEmitter(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, ellipsoid, comp);
					ellipsoid.LoadFrom(stream);
					return ellipsoid;
				}
			case UnityClassID.GameObject:
				{
					GameObject gameObj = new GameObject(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, gameObj, comp);
					gameObj.LoadFrom(stream);
					return gameObj;
				}
			case UnityClassID.Material:
				{
					Material mat = new Material(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, mat, comp);
					mat.LoadFrom(stream);
					return mat;
				}
			case UnityClassID.Mesh:
				{
					if (loadingReferencials)
					{
						return comp;
					}
					Mesh mesh = new Mesh(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, mesh, comp);
					mesh.LoadFrom(stream);
					return mesh;
				}
			case UnityClassID.MeshFilter:
				{
					MeshFilter meshFilter = new MeshFilter(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, meshFilter, comp);
					meshFilter.LoadFrom(stream);
					return meshFilter;
				}
			case UnityClassID.MeshRenderer:
				{
					MeshRenderer meshRenderer = new MeshRenderer(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, meshRenderer, comp);
					meshRenderer.LoadFrom(stream);
					return meshRenderer;
				}
			case UnityClassID.ParticleAnimator:
				{
					ParticleAnimator particleAnimator = new ParticleAnimator(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, particleAnimator, comp);
					particleAnimator.LoadFrom(stream);
					return particleAnimator;
				}
			case UnityClassID.ParticleRenderer:
				{
					ParticleRenderer particleRenderer = new ParticleRenderer(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, particleRenderer, comp);
					particleRenderer.LoadFrom(stream);
					return particleRenderer;
				}
			default:
				if (comp.classID2 == UnityClassID.MonoBehaviour)
				{
					if (loadingReferencials)
					{
						return comp;
					}
					try
					{
						MonoBehaviour monoBehaviour = new MonoBehaviour(this, comp.pathID, comp.classID1, comp.classID2);
						ReplaceSubfile(index, monoBehaviour, comp);
						monoBehaviour.LoadFrom(stream);
						return monoBehaviour;
					}
					catch (Exception e)
					{
						ReplaceSubfile(index, comp, comp);
						comp.replacement = null;
						if (!reported.Contains(e.Message))
						{
							Utility.ReportException(e);
							reported.Add(e.Message);
						}
						return null;
					}
				}
				else
				{
					string message = "Unhandled class: " + comp.classID1 + "/" + comp.classID2;
					if (!reported.Contains(message))
					{
						Report.ReportLog(message);
						reported.Add(message);
					}
				}
				break;
			case UnityClassID.Shader:
				{
					Shader shader = new Shader(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, shader, comp);
					shader.LoadFrom(stream);
					return shader;
				}
			case UnityClassID.SkinnedMeshRenderer:
				{
					SkinnedMeshRenderer sMesh = new SkinnedMeshRenderer(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, sMesh, comp);
					sMesh.LoadFrom(stream);
					return sMesh;
				}
			case UnityClassID.Sprite:
				{
					Sprite sprite = new Sprite(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, sprite, comp);
					sprite.LoadFrom(stream);
					return sprite;
				}
			case UnityClassID.TextAsset:
				{
					if (loadingReferencials)
					{
						return comp;
					}
					TextAsset ta = new TextAsset(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, ta, comp);
					ta.LoadFrom(stream);
					return ta;
				}
			case UnityClassID.Texture2D:
				{
					if (loadingReferencials)
					{
						return comp;
					}
					Texture2D tex = new Texture2D(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, tex, comp);
					tex.LoadFrom(stream);
					return tex;
				}
			case UnityClassID.Transform:
				{
					Transform trans = new Transform(this, comp.pathID, comp.classID1, comp.classID2);
					ReplaceSubfile(index, trans, comp);
					trans.LoadFrom(stream);
					return trans;
				}
			}
			return null;
		}

		public void WritePPtr(Component comp, bool differentClass, Stream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);
			writer.Write(differentClass ? 1 : 0);
			writer.Write(comp != null ? comp.pathID : 0);
		}

		public void RemoveSubfile(Component asset)
		{
			if (Components.Remove(asset))
			{
				asset.pathID = 0;
				if (!(asset is NotLoaded))
				{
					foreach (NotLoaded replaced in RemovedList)
					{
						if (replaced.replacement == asset)
						{
							replaced.replacement = null;
							replaced.pathID = 0;
							break;
						}
					}
				}
			}
		}

		public void ReplaceSubfile(int index, Component file, NotLoaded replaced)
		{
			if (index >= 0)
			{
				Components.RemoveAt(index);
				replaced.replacement = file;
				RemovedList.Add(replaced);
			}
			else
			{
				for (int i = Components.Count - 1; i >= 0; i--)
				{
					if (Components[i].classID1 == file.classID1)
					{
						index = i + 1;
						break;
					}
				}
				if (index < 0)
				{
					index = Components.Count;
				}
			}
			Components.Insert(index, file);
		}

		public static string ToString(Component subfile)
		{
			Type t = subfile.GetType();
			PropertyInfo info = t.GetProperty("m_Name");
			if (info != null)
			{
				return info.GetValue(subfile, null).ToString();
			}
			else
			{
				info = t.GetProperty("m_GameObject");
				if (info != null)
				{
					PPtr<GameObject> gameObjPtr = info.GetValue(subfile, null) as PPtr<GameObject>;
					if (gameObjPtr != null)
					{
						return gameObjPtr.instance.m_Name;
					}
					else
					{
						GameObject gameObj = info.GetValue(subfile, null) as GameObject;
						if (gameObj != null)
						{
							return gameObj.m_Name;
						}
						throw new Exception("What reference is this!? " + subfile.pathID + " " + subfile.classID1);
					}
				}
				throw new Exception("Neither m_Name nor m_GameObject member " + subfile.pathID + " " + subfile.classID1);
			}
		}
	}
}