using System.IO;
using System.Text;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Internal;
using G3MLib.Modding.Logging;

namespace G3MLib.Modding.Resources;

/// <summary>
/// An audio group file (audiogroupN.dat). Only the AUDO entry table is read on open; entry bytes are read when asked for.
/// Other chunks are copied unchanged on save. Not thread safe per instance.
/// </summary>
public sealed class AudioGroupFile : IDisposable
{
	private sealed class Entry
	{
		public long Offset;
		public int Length;
		public ReadOnlyMemory<byte>? Data;
	}

	private sealed record Chunk(string Name, long Offset, int Length);

	private FileStream _stream;
	private string _path;
	private List<Chunk> _chunks = [];
	private List<Entry> _entries = [];
	private byte[] _trailing = [];
	private bool _padLastEntry = true;

	private AudioGroupFile(string path, FileStream stream)
	{
		_path = Path.GetFullPath(path);
		_stream = stream;
	}

	public int Count => _entries.Count;

	public static AudioGroupFile Open(string path, CancellationToken cancellationToken = default)
	{
		FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		AudioGroupFile file = new AudioGroupFile(path, stream);
		try
		{
			file.ReadTable(cancellationToken);
			return file;
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	private void ReadTable(CancellationToken cancellationToken)
	{
		_chunks = [];
		_entries = [];
		using BinaryReader reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);
		_stream.Position = 0;
		if (_stream.Length < 8 || new string(reader.ReadChars(4)) != "FORM")
			throw new InvalidDataException("Not an audio group file: missing FORM header.");
		long end = Math.Min(_stream.Length, 8L + reader.ReadUInt32());
		while (_stream.Position + 8 <= end)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string name = new string(reader.ReadChars(4));
			int length = checked((int)reader.ReadUInt32());
			long start = _stream.Position;
			if (start + length > end)
				throw new InvalidDataException($"Chunk {name} exceeds the file.");
			_chunks.Add(new Chunk(name, start, length));
			if (name == "AUDO")
			{
				uint count = reader.ReadUInt32();
				if ((long)count * 4 > length - 4)
					throw new InvalidDataException("The AUDO pointer table exceeds the chunk.");
				long[] pointers = new long[count];
				for (int i = 0; i < count; i++)
					pointers[i] = reader.ReadUInt32();
				foreach (long pointer in pointers)
				{
					if (pointer < start || pointer + 4 > start + length)
						throw new InvalidDataException("An AUDO entry lies outside its chunk.");
					_stream.Position = pointer;
					int size = checked((int)reader.ReadUInt32());
					if (pointer + 4 + size > start + length)
						throw new InvalidDataException("An AUDO entry exceeds its chunk.");
					_entries.Add(new Entry { Offset = pointer + 4, Length = size });
				}
				// The last entry of the file's last chunk may come without its padding; keep what the file does.
				if (_entries.Count > 0)
				{
					long lastEnd = _entries[^1].Offset + _entries[^1].Length;
					_padLastEntry = start + length >= ((lastEnd + 3) & ~3L);
				}
			}
			_stream.Position = start + length;
		}
		_trailing = new byte[_stream.Length - end];
		_stream.Position = end;
		_stream.ReadExactly(_trailing);
		if (!_chunks.Exists(chunk => chunk.Name == "AUDO"))
			throw new InvalidDataException("The file has no AUDO chunk.");
	}

	private Entry Get(int id)
	{
		if (id < 0 || id >= _entries.Count)
			throw new ArgumentOutOfRangeException(nameof(id));
		return _entries[id];
	}

	public ReadOnlyMemory<byte> GetEntry(int id)
	{
		Entry entry = Get(id);
		if (entry.Data is { } data)
			return data;
		byte[] bytes = new byte[entry.Length];
		_stream.Position = entry.Offset;
		_stream.ReadExactly(bytes);
		return bytes;
	}

	public int Add(ReadOnlyMemory<byte> data)
	{
		_entries.Add(new Entry { Data = data, Length = data.Length });
		return _entries.Count - 1;
	}

	public void Replace(int id, ReadOnlyMemory<byte> data)
	{
		Entry entry = Get(id);
		entry.Data = data;
		entry.Length = data.Length;
	}

	/// <summary>Empties the entry. Ids stay as they are until <see cref="Compact"/>.</summary>
	public void Remove(int id) => Replace(id, ReadOnlyMemory<byte>.Empty);

	/// <summary>Drops empty entries and renumbers the rest. Returns old id to new id for every kept entry.</summary>
	public IReadOnlyDictionary<int, int> Compact()
	{
		Dictionary<int, int> map = [];
		List<Entry> kept = [];
		for (int i = 0; i < _entries.Count; i++)
		{
			if (_entries[i].Length == 0)
				continue;
			map[i] = kept.Count;
			kept.Add(_entries[i]);
		}
		_entries = kept;
		return map;
	}

	/// <summary>Writes the file to a temporary file and moves it over <paramref name="path"/>. With <paramref name="backup"/> an existing file is kept as path.bak.</summary>
	public void Save(string path, bool backup, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
	{
		string temp = path + ".g3mtmp";
		try
		{
			using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write))
			using (BinaryWriter writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true))
			{
				writer.Write("FORM"u8);
				writer.Write(0u);
				for (int i = 0; i < _chunks.Count; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					Chunk chunk = _chunks[i];
					writer.Write(Encoding.ASCII.GetBytes(chunk.Name));
					writer.Write(0u);
					long start = output.Position;
					if (chunk.Name == "AUDO")
						WriteAudo(output, writer, progress, cancellationToken);
					else
						CopyRaw(output, chunk);
					long finish = output.Position;
					output.Position = start - 4;
					writer.Write(checked((uint)(finish - start)));
					output.Position = finish;
				}
				output.Position = 4;
				writer.Write(checked((uint)(output.Length - 8)));
				output.Position = output.Length;
				output.Write(_trailing);
			}
			bool inPlace = string.Equals(Path.GetFullPath(path), _path, StringComparison.OrdinalIgnoreCase);
			if (inPlace)
				_stream.Dispose();
			if (backup && File.Exists(path))
				File.Copy(path, path + ".bak", overwrite: true);
			File.Move(temp, path, overwrite: true);
			if (inPlace)
			{
				_stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				ReadTable(cancellationToken);
			}
		}
		catch
		{
			if (File.Exists(temp))
				File.Delete(temp);
			throw;
		}
	}

	private void CopyRaw(FileStream output, Chunk chunk)
	{
		byte[] bytes = new byte[chunk.Length];
		_stream.Position = chunk.Offset;
		_stream.ReadExactly(bytes);
		output.Write(bytes);
	}

	private void WriteAudo(FileStream output, BinaryWriter writer, IProgress<double>? progress, CancellationToken cancellationToken)
	{
		long table = output.Position;
		writer.Write((uint)_entries.Count);
		writer.Write(new byte[_entries.Count * 4]);
		for (int i = 0; i < _entries.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			long entryStart = output.Position;
			ReadOnlyMemory<byte> data = GetEntry(i);
			writer.Write((uint)data.Length);
			output.Write(data.Span);
			while (output.Position % 4 != 0 && (i < _entries.Count - 1 || _padLastEntry))
				output.WriteByte(0);
			long after = output.Position;
			output.Position = table + 4 + (i * 4);
			writer.Write(checked((uint)entryStart));
			output.Position = after;
			progress?.Report((i + 1d) / _entries.Count);
		}
	}

	public void Dispose() => _stream.Dispose();
}

/// <summary>Rewrites the audio ids of sounds after <see cref="AudioGroupFile.Compact"/>.</summary>
public static class SoundIdRemap
{
	/// <summary>Sounds of the group whose id is in the map get the new id; sounds whose entry was dropped get -1.</summary>
	public static void Apply(GameMakerData data, int groupId, IReadOnlyDictionary<int, int> map)
	{
		if (data.Sounds == null)
			return;
		foreach (GameMakerSound sound in data.Sounds)
		{
			if (sound == null || sound.GroupID != groupId || sound.AudioID < 0)
				continue;
			sound.AudioID = map.TryGetValue(sound.AudioID, out int next) ? next : -1;
		}
	}
}

public enum AudioSource
{
	None,
	Embedded,
	GroupFile
}

/// <summary>Finds the bytes of sounds, opening each audio group file once. Safe to call from several threads.</summary>
public sealed class AudioGroupReader : IDisposable
{
	private readonly GameMakerData _data;
	private readonly string _dataDirectory;
	private readonly Dictionary<int, AudioGroupFile?> _files = [];

	public AudioGroupReader(GameMakerData data, string dataFilePath)
	{
		_data = data;
		_dataDirectory = Path.GetDirectoryName(dataFilePath) ?? string.Empty;
	}

	public ReadOnlyMemory<byte>? GetAudio(GameMakerSound sound, out AudioSource source)
	{
		byte[]? embedded = sound.AudioFile?.Data;
		bool hasEmbedded = embedded is { Length: > 0 };
		if (hasEmbedded && (sound.GroupID > 0 || sound.AudioID < 0))
		{
			source = AudioSource.Embedded;
			return embedded;
		}
		source = AudioSource.None;
		if (sound.GroupID <= 0 || _data.AudioGroups == null || sound.GroupID >= _data.AudioGroups.Count || sound.AudioID < 0)
			return null;
		AudioGroupFile? file = GetFile(sound.GroupID);
		if (file == null)
			return null;
		lock (file)
		{
			if (sound.AudioID >= file.Count)
				return null;
			ReadOnlyMemory<byte> bytes = file.GetEntry(sound.AudioID);
			if (bytes.Length == 0)
				return null;
			source = AudioSource.GroupFile;
			return bytes;
		}
	}

	private AudioGroupFile? GetFile(int groupId)
	{
		lock (_files)
		{
			if (_files.TryGetValue(groupId, out AudioGroupFile? cached))
				return cached;
			string? path = ResourceAssetUtil.ResolveAudioGroupPath(_data, _dataDirectory, groupId);
			AudioGroupFile? file = null;
			if (path != null && File.Exists(path))
			{
				try
				{
					file = AudioGroupFile.Open(path);
				}
				catch (Exception ex) when (!ModdingCancellation.IsCancellation(ex))
				{
					file = null;
				}
			}
			_files[groupId] = file;
			return file;
		}
	}

	public void Dispose()
	{
		foreach (AudioGroupFile? file in _files.Values)
			file?.Dispose();
		_files.Clear();
	}
}
