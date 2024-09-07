﻿using NAudio.Dsp;

public class Equalizer
{
    private readonly BiQuadFilter[] _filters;

    public bool IsActive { get; set; }

    public Equalizer(int sampleRate, int numberOfBands)
    {
        IsActive = true;
        _filters = new BiQuadFilter[numberOfBands];

        // Inicializace filtrů
        for (int i = 0; i < numberOfBands; i++)
        {
            float frequency = GetFrequencyForBand(i, numberOfBands);
            _filters[i] = BiQuadFilter.PeakingEQ(sampleRate, frequency, 0.7f, 0f);  // Inicializujeme s neutrálním ziskem (0 dB)
        }
    }

    // Nastavení zisku pro pásmo
    public void SetGain(int bandIndex, float gain)
    {
        if (bandIndex < 0 || bandIndex >= _filters.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(bandIndex), "Neplatný index pásma");
        }

        float frequency = GetFrequencyForBand(bandIndex, _filters.Length);
        _filters[bandIndex] = BiQuadFilter.PeakingEQ(44100, frequency, 0.7f, gain);  // Změna zisku
    }

    // Aplikace ekvalizéru na buffer
    // public void ApplyEqualization(Span<int> leftChannelBuffer, Span<int> rightChannelBuffer, int samplesCount)
    // {
    //     if (IsActive is false)
    //     {
    //         return;
    //     }
    //
    //     for (int i = 0; i < samplesCount; i++)
    //     {
    //         float leftSample = leftChannelBuffer[i];
    //         float rightSample = rightChannelBuffer[i];
    //
    //         // Aplikace filtrů na vzorky
    //         for (int j = 0; j < _filters.Length; j++)
    //         {
    //             leftSample = _filters[j].Transform(leftSample);
    //             rightSample = _filters[j].Transform(rightSample);
    //         }
    //
    //         leftChannelBuffer[i] = (int)leftSample;
    //         rightChannelBuffer[i] = (int)rightSample;
    //     }
    // }
    public void ApplyEqualization(Span<int> leftChannelBuffer, Span<int> rightChannelBuffer, int samplesCount)
    {
        if (IsActive is false)
        {
            return;
        }

        float leftTotalGain = 1.0f;
        float rightTotalGain = 1.0f;

        // Projdeme všechny samply a aplikujeme filtry
        for (int i = 0; i < samplesCount; i++)
        {
            float leftSample = leftChannelBuffer[i];
            float rightSample = rightChannelBuffer[i];

            // Aplikace filtrů na vzorky
            for (int j = 0; j < _filters.Length; j++)
            {
                leftSample = _filters[j].Transform(leftSample);
                rightSample = _filters[j].Transform(rightSample);
            }

            // Uložíme nový výsledek zpět do bufferu
            leftChannelBuffer[i] = (int)(leftSample * leftTotalGain);
            rightChannelBuffer[i] = (int)(rightSample * rightTotalGain);
        }

        // Kompenzace hlasitosti (možno upravit podle celkového zesílení/zeslabení)
        float gainCompensation = CalculateGainCompensation();
        for (int i = 0; i < samplesCount; i++)
        {
            leftChannelBuffer[i] = (int)(leftChannelBuffer[i] * gainCompensation);
            rightChannelBuffer[i] = (int)(rightChannelBuffer[i] * gainCompensation);
        }
    }

// Funkce pro výpočet kompenzace
    private float CalculateGainCompensation()
    {
        // Zde můžete přidat logiku, která bude měřit průměrný zisk filtru
        // a na základě toho upravit celkový gain
        // Například můžete snížit výstupní hlasitost, pokud je příliš vysoká.
        return 0.8f; // Příklad kompenzace hlasitosti, upravte podle potřeby
    }

    // Vypočítá frekvenci pro pásmo
    private float GetFrequencyForBand(int bandIndex, int numberOfBands)
    {
        float minFreq = 20f; // Dolní frekvence
        float maxFreq = 20000f; // Horní frekvence
        return minFreq * (float)Math.Pow(maxFreq / minFreq, (float)bandIndex / (numberOfBands - 1));
    }
}