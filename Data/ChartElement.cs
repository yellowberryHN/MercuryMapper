using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Shapes;
using MercuryMapper.Enums;
using MercuryMapper.Utils;

namespace MercuryMapper.Data;

public class BeatData
{
    public readonly int Measure;
    public readonly int Tick;
    public int FullTick => Measure * 1920 + Tick;
    
    public readonly float MeasureDecimal;
    
    public BeatData(int measure, int tick)
    {
        // integer division, floors to 0 if tick < 1920.
        Measure = measure + tick / 1920;
        Tick = MathExtensions.Modulo(tick, 1920);
        MeasureDecimal = GetMeasureDecimal(measure, tick);
    }

    public BeatData(float measureDecimal)
    {
        Measure = (int)measureDecimal;
        Tick = (int)MathF.Round((measureDecimal - Measure) * 1920);
        MeasureDecimal = measureDecimal;
    }
    
    public BeatData(int fullTick)
    {
        Measure = fullTick / 1920;
        Tick = fullTick - Measure * 1920;
        MeasureDecimal = GetMeasureDecimal(Measure, Tick);
    }

    public BeatData(BeatData data)
    {
        // integer division, floors to 0 if tick < 1920.
        Measure = data.Measure + data.Tick / 1920;
        Tick = MathExtensions.Modulo(data.Tick, 1920);
        MeasureDecimal = GetMeasureDecimal(Measure, Tick);
    }

    public static float GetMeasureDecimal(int measure, int tick)
    {
        return measure + tick / 1920.0f;
    }
}

public class TimeScaleData
{
    public float MeasureDecimal { get; set; }
    public float ScaledMeasureDecimal { get; set; }
    public float ScaledMeasureDecimalHiSpeed { get; set; }

    public float HiSpeed { get; set; }
    public float TimeSigRatio { get; set; }
    public float BpmRatio { get; set; }

    public bool IsLast { get; set; }
}

public class TimeSig(int upper, int lower)
{
    public readonly int Upper = upper;
    public readonly int Lower = lower;
    public float Ratio => Upper / (float)Lower;

    public TimeSig(TimeSig timeSig) : this(timeSig.Upper, timeSig.Lower) { }
}

public class ChartElement
{
    public BeatData BeatData { get; set; } = new(-1, 0);
    public GimmickType GimmickType { get; set; } = GimmickType.None;
    public Guid Guid { get; set; } = Guid.NewGuid();
}

public class Gimmick : ChartElement
{
    public float Bpm { get; set; }
    public TimeSig TimeSig { get; set; } = new(4, 4);
    public float HiSpeed { get; set; }
    public float TimeStamp { get; set; }

    public Gimmick() { }
    
    public Gimmick(BeatData beatData, GimmickType gimmickType, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = beatData;
        GimmickType = gimmickType;
    }

    public Gimmick(Gimmick gimmick, Guid? guid = null) : this(gimmick.BeatData, gimmick.GimmickType)
    {
        Guid = guid ?? Guid;
        switch (GimmickType)
        {
            case GimmickType.BpmChange: Bpm = gimmick.Bpm; break;
            case GimmickType.TimeSigChange: TimeSig = new TimeSig(gimmick.TimeSig); break;
            case GimmickType.HiSpeedChange: HiSpeed = gimmick.HiSpeed; break;
        }
    }

    public Gimmick(int measure, int tick, GimmickType gimmickType, string value1, string value2, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = new(measure, tick);
        GimmickType = gimmickType;

        switch (GimmickType)
        {
            case GimmickType.BpmChange:
                Bpm = Convert.ToSingle(value1, CultureInfo.InvariantCulture);
                break;
            case GimmickType.TimeSigChange:
                TimeSig = new(Convert.ToInt32(value1, CultureInfo.InvariantCulture), Convert.ToInt32(value2, CultureInfo.InvariantCulture));
                break;
            case GimmickType.HiSpeedChange:
                HiSpeed = Convert.ToSingle(value1, CultureInfo.InvariantCulture);
                break;
        }
    }

    public bool IsReverse => GimmickType is GimmickType.ReverseEffectStart or GimmickType.ReverseEffectEnd or GimmickType.ReverseNoteEnd;
    public bool IsStop => GimmickType is GimmickType.StopStart or GimmickType.StopEnd;

    public string ToNetworkString()
    {
        string result = $"{Guid} {BeatData.Measure:F0} {BeatData.Tick:F0} {(int)GimmickType:F0}";
        
        result += GimmickType switch
        {
            GimmickType.BpmChange => $" {Bpm.ToString("F6", CultureInfo.InvariantCulture)}\n",
            GimmickType.HiSpeedChange => $" {HiSpeed.ToString("F6", CultureInfo.InvariantCulture)}\n",
            GimmickType.TimeSigChange => $" {TimeSig.Upper:F0} {TimeSig.Lower:F0}\n",
            _ => "\n"
        };
        
        return result;
    }
    
    public static Gimmick ParseNetworkString(string[] data)
    {
        Gimmick gimmick = new()
        {
            Guid = Guid.Parse(data[0]),
            BeatData = new(Convert.ToInt32(data[1]), Convert.ToInt32(data[2])),
            GimmickType = (GimmickType)Convert.ToInt32(data[3]),
        };

        if (gimmick.GimmickType == GimmickType.BpmChange && data.Length == 5) gimmick.Bpm = Convert.ToSingle(data[4]);
        if (gimmick.GimmickType == GimmickType.HiSpeedChange && data.Length == 5) gimmick.HiSpeed = Convert.ToSingle(data[4]);
        if (gimmick.GimmickType == GimmickType.TimeSigChange && data.Length == 6) gimmick.TimeSig = new(Convert.ToInt32(data[4]), Convert.ToInt32(data[5]));

        return gimmick;
    }
}

public class Note : ChartElement
{
    public NoteType NoteType { get; set; } = NoteType.Touch;
    public BonusType BonusType { get; set; }
    public int Position { get; set; }
    public int Size { get; set; }

    public bool RenderSegment { get; set; } = true;
    public MaskDirection MaskDirection { get; set; }
    public Note? NextReferencedNote { get; set; }
    public Note? PrevReferencedNote { get; set; }

    public int ParsedIndex { get; set; }

    public Note() { }

    public Note(BeatData beatData, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = beatData;
    }

    public Note(Note note, Guid? guid = null) : this(note.BeatData)
    {
        Guid = guid ?? Guid;
        Position = note.Position;
        Size = note.Size;
        NoteType = note.NoteType;
        BonusType = note.BonusType;
        RenderSegment = note.RenderSegment;
        MaskDirection = note.MaskDirection;

        NextReferencedNote = note.NextReferencedNote;
        PrevReferencedNote = note.PrevReferencedNote;
    }

    public Note(int measure, int tick, NoteType noteType, BonusType bonusType, int noteIndex, int position, int size, bool renderSegment, Guid? guid = null)
    {
        Guid = guid ?? Guid;
        BeatData = new(measure, tick);
        GimmickType = GimmickType.None;
        NoteType = noteType;
        BonusType = bonusType;
        Position = position;
        Size = size;
        RenderSegment = renderSegment;

        ParsedIndex = noteIndex;
    }
    
    public bool IsHold => NoteType
        is NoteType.HoldStart 
        or NoteType.HoldSegment 
        or NoteType.HoldEnd;

    public bool IsSegment => NoteType
        is NoteType.HoldSegment
        or NoteType.HoldEnd;

    public bool IsChain => NoteType
        is NoteType.Chain;
    
    public bool IsSlide => NoteType 
        is NoteType.SlideClockwise 
        or NoteType.SlideCounterclockwise;

    public bool IsSnap => NoteType 
        is NoteType.SnapForward 
        or NoteType.SnapBackward;

    public bool IsBonus => BonusType is BonusType.Bonus;

    public bool IsRNote => BonusType is BonusType.RNote;

    public bool IsMask => NoteType
        is NoteType.MaskAdd
        or NoteType.MaskRemove;

    public static int MinSize(NoteType noteType, BonusType bonusType)
    {
        return (noteType, bonusType) switch
        {
            (NoteType.Touch, BonusType.None) => 4,
            (NoteType.Touch, BonusType.Bonus) => 5,
            (NoteType.Touch, BonusType.RNote) => 6,
            
            (NoteType.SnapForward, BonusType.None) => 6,
            (NoteType.SnapBackward, BonusType.None) => 6,
            
            (NoteType.SnapForward, BonusType.Bonus) => 6,
            (NoteType.SnapBackward, BonusType.Bonus) => 6,
            
            (NoteType.SnapForward, BonusType.RNote) => 8,
            (NoteType.SnapBackward, BonusType.RNote) => 8,
            
            (NoteType.SlideClockwise, BonusType.None) => 5,
            (NoteType.SlideCounterclockwise, BonusType.None) => 5,
            
            (NoteType.SlideClockwise, BonusType.Bonus) => 7,
            (NoteType.SlideCounterclockwise, BonusType.Bonus) => 7,
            
            (NoteType.SlideClockwise, BonusType.RNote) => 10,
            (NoteType.SlideCounterclockwise, BonusType.RNote) => 10,
            
            (NoteType.Chain, BonusType.None) => 4,
            (NoteType.Chain, BonusType.Bonus) => 4,
            (NoteType.Chain, BonusType.RNote) => 10,
            
            (NoteType.HoldStart, BonusType.None) => 2,
            (NoteType.HoldStart, BonusType.Bonus) => 2,
            (NoteType.HoldStart, BonusType.RNote) => 8,
            
            (NoteType.HoldSegment, _) => 1,
            (NoteType.HoldEnd, _) => 1,
            
            (NoteType.MaskAdd, _) => 1,
            (NoteType.MaskRemove, _) => 1,
            
            (NoteType.EndOfChart, _) => 60,
            
            _ => 5
        };
    }

    public IEnumerable<Note> References()
    {
        List<Note> refs = [this];
        if (!IsHold) return refs;

        Note? prev = PrevReferencedNote;
        Note? next = NextReferencedNote;

        while (prev is not null)
        {
            refs.Add(prev);
            prev = prev.PrevReferencedNote;
        }

        while (next is not null)
        {
            refs.Add(next);
            next = next.NextReferencedNote;
        }

        return refs.OrderBy(x => x.BeatData.FullTick);
    }

    public Note? FirstReference()
    {
        if (!IsHold) return null;
        
        Note? first = this;
        Note? prev = PrevReferencedNote;
        
        while (prev is not null)
        {
            first = prev;
            prev = prev.PrevReferencedNote;
        }

        return first;
    }

    public string ToNetworkString()
    {
        string result = $"{Guid} {BeatData.Measure:F0} {BeatData.Tick:F0} {(int)NoteType:F0} {(int)BonusType:F0} {Position:F0} {Size:F0} {(RenderSegment ? 1 : 0)}";
        if (IsMask)
        {
            result += $" {(int)MaskDirection:F0}";
        }
        else
        {
            result += $" {(NextReferencedNote != null ? NextReferencedNote.Guid : "null")}";
            result += $" {(PrevReferencedNote != null ? PrevReferencedNote.Guid : "null")}";
        }
        
        return result;
    }
    
    public static Note ParseNetworkString(Chart chart, string[] data)
    {
        Note note = new()
        {
            Guid = Guid.Parse(data[0]),
            BeatData = new(Convert.ToInt32(data[1]), Convert.ToInt32(data[2])),
            NoteType = (NoteType)Convert.ToInt32(data[3]),
            BonusType = (BonusType)Convert.ToInt32(data[4]),
            Position = Convert.ToInt32(data[5]),
            Size = Convert.ToInt32(data[6]),
            RenderSegment = data[7] != "0",
        };

        if (note.IsMask && data.Length == 9) note.MaskDirection = (MaskDirection)Convert.ToInt32(data[8]);

        if (data.Length == 10)
        {
            if (data[7] != "null") note.NextReferencedNote = chart.FindNoteByGuid(data[7]);
            if (data[8] != "null") note.PrevReferencedNote = chart.FindNoteByGuid(data[8]);
        }

        return note;
    }

    public int NoteToId()
    {
        return (NoteType, BonusType) switch
        {
            (NoteType.Touch, BonusType.None) => 1,
            (NoteType.Touch, BonusType.Bonus) => 2,
            (NoteType.Touch, BonusType.RNote) => 20,

            (NoteType.SnapForward, BonusType.None) => 3,
            (NoteType.SnapForward, BonusType.Bonus) => 3,
            (NoteType.SnapForward, BonusType.RNote) => 21,

            (NoteType.SnapBackward, BonusType.None) => 4,
            (NoteType.SnapBackward, BonusType.Bonus) => 4,
            (NoteType.SnapBackward, BonusType.RNote) => 22,

            (NoteType.SlideClockwise, BonusType.None) => 5,
            (NoteType.SlideClockwise, BonusType.Bonus) => 6,
            (NoteType.SlideClockwise, BonusType.RNote) => 23,

            (NoteType.SlideCounterclockwise, BonusType.None) => 7,
            (NoteType.SlideCounterclockwise, BonusType.Bonus) => 8,
            (NoteType.SlideCounterclockwise, BonusType.RNote) => 24,

            (NoteType.HoldStart, BonusType.None) => 9,
            (NoteType.HoldStart, BonusType.Bonus) => 9,
            (NoteType.HoldStart, BonusType.RNote) => 25,

            (NoteType.HoldSegment, _) => 10,
            (NoteType.HoldEnd, _) => 11,

            (NoteType.MaskAdd, _) => 12,
            (NoteType.MaskRemove, _) => 13,

            (NoteType.EndOfChart, _) => 14,

            (NoteType.Chain, BonusType.None) => 16,
            (NoteType.Chain, BonusType.Bonus) => 16,
            (NoteType.Chain, BonusType.RNote) => 26,
            _ => 1
        };
    }
    
    public static NoteType NoteTypeFromId(int id)
    {
        return id switch
        {
            1 or 2 or 20 => NoteType.Touch,
            3 or 21 => NoteType.SnapForward,
            4 or 22 => NoteType.SnapBackward,
            5 or 6 or 23 => NoteType.SlideClockwise,
            7 or 8 or 24 => NoteType.SlideCounterclockwise,
            9 or 25 => NoteType.HoldStart,
            10 => NoteType.HoldSegment,
            11 => NoteType.HoldEnd,
            12 => NoteType.MaskAdd,
            13 => NoteType.MaskRemove,
            14 => NoteType.EndOfChart,
            16 or 26 => NoteType.Chain,
            _ => NoteType.None
        };
    }

    public static BonusType BonusTypeFromId(int id)
    {
        return id switch
        {
            1 or 3 or 4 or 5 or 7 or 9 or 10 or 11 or 12 or 13 or 14 or 16 => BonusType.None,
            2 or 6 or 8 => BonusType.Bonus,
            20 or 21 or 22 or 23 or 24 or 25 or 26 => BonusType.RNote,
            _ => BonusType.None
        };
    }
}

public struct Hold()
{
    public List<Note> Segments = [];
}

public class Comment(Guid guid, BeatData beatData, string text, Rectangle marker)
{
    public Guid Guid = guid;
    public BeatData BeatData = beatData;
    public string Text = text;
    public Rectangle Marker = marker;
}