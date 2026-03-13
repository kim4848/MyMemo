import { describe, it, expect } from 'vitest';
import { extractSpeakerLabels } from './SpeakerRenamePanel';

describe('extractSpeakerLabels', () => {
  it('extracts unique speaker labels from transcription texts', () => {
    const texts = [
      { rawText: 'Speaker 0: Hello\nSpeaker 1: Hi there' },
      { rawText: 'Speaker 0: How are you?\nSpeaker 2: Fine' },
    ];
    const labels = extractSpeakerLabels(texts);
    expect(labels).toEqual(['Speaker 2', 'Speaker 1', 'Speaker 0']);
  });

  it('returns empty array when no speaker labels found', () => {
    const texts = [{ rawText: 'Just some text without speakers' }];
    expect(extractSpeakerLabels(texts)).toEqual([]);
  });

  it('handles Speaker 10+ without confusion with Speaker 1', () => {
    const texts = [
      { rawText: 'Speaker 1: Hi\nSpeaker 10: Hello\nSpeaker 2: Hey' },
    ];
    const labels = extractSpeakerLabels(texts);
    // Higher numbers first to avoid partial match issues
    expect(labels).toEqual(['Speaker 10', 'Speaker 2', 'Speaker 1']);
  });

  it('handles empty texts array', () => {
    expect(extractSpeakerLabels([])).toEqual([]);
  });

  it('handles empty rawText strings', () => {
    const texts = [{ rawText: '' }];
    expect(extractSpeakerLabels(texts)).toEqual([]);
  });
});
