using System.Collections.Generic;
using System.Linq;
using Cardstorm.Spawning;
using NUnit.Framework;

namespace Cardstorm.Tests
{
    public class SpawnCurveCsvTests
    {
        static readonly string[] TypeNames = { "Rondador", "Corredor", "Tirador", "Coloso" };

        // Así exporta Excel en español las hojas Tramos y Oleadas: separador ';' y coma decimal.
        const string TramosCsv =
            "Inicio (s);Fin (s);Rondadores/min;Corredores/min;Tiradores/min;Colosos/min;Refuerzo f;Nombre\r\n" +
            "0;60;40;0;0;0;1;Aprendizaje\r\n" +
            "60;120;54;0;0;0;1;Aprendizaje\r\n" +
            "120;165;54;10;0;0;1;Primer pico\r\n" +
            "165;195;54;10;0;0;1;Oleada 1\r\n" +
            "195;240;40;6;0;0;1;Respiro\r\n" +
            "240;300;60;10;8;0;1,1;Primer pico\r\n" +
            "300;360;40;6;4;0;1,1;Heraldo\r\n" +
            "360;390;36;6;4;0;1,1;Respiro\r\n" +
            "390;480;72;28;9;2;1,2;Construcción\r\n" +
            "480;510;72;28;9;2;1,2;Oleada 2\r\n" +
            "510;540;44;12;6;1;1,2;Respiro\r\n" +
            "540;660;88;38;12;3;1,3;Construcción\r\n" +
            "660;690;88;38;12;3;1,3;Oleada 3\r\n" +
            "690;720;54;18;8;2;1,3;Respiro\r\n" +
            "720;840;112;50;18;4;1,4;Clímax\r\n" +
            "840;870;112;50;18;4;1,4;Oleada 4\r\n" +
            "870;900;0;0;0;0;1,4;Calma\r\n";

        const string OleadasCsv =
            "﻿Inicio (s);Rondadores extra;Corredores extra;Tiradores extra;Colosos extra;Cofre al terminar (oro);Hora del cofre (s);Nombre\r\n" +
            "165;30;0;0;0;15;195;Oleada 1: Cerco\r\n" +
            "480;0;20;8;0;20;510;Oleada 2: Pinza\r\n" +
            "660;40;0;0;1;25;690;Oleada 3: Estampida\r\n" +
            "840;30;8;4;1;25;870;Oleada 4: Tormenta\r\n" +
            ";;;;;;;\r\n" +
            "Cada oleada reparte sus enemigos extra en 30 s; el cofre aparece al empezar el respiro.;;;;;;;\r\n";

        [Test]
        public void ParseSegments_SpanishExport_MatchesDefaults()
        {
            var errors = new List<string>();
            var segments = SpawnCurveCsv.ParseSegments(TramosCsv, TypeNames, errors);

            CollectionAssert.IsEmpty(errors);
            var expected = SpawnCurveDefaults.Segments();
            Assert.AreEqual(expected.Count, segments.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].name, segments[i].name);
                Assert.AreEqual(expected[i].startTime, segments[i].startTime);
                Assert.AreEqual(expected[i].endTime, segments[i].endTime);
                Assert.AreEqual(expected[i].reinforcement, segments[i].reinforcement, 1e-5f);
                CollectionAssert.AreEqual(expected[i].ratePerMinute, segments[i].ratePerMinute);
            }
        }

        [Test]
        public void ParseWaves_SpanishExportWithBomAndFooter_MatchesDefaults()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var waves = SpawnCurveCsv.ParseWaves(OleadasCsv, TypeNames, null, errors, warnings);

            CollectionAssert.IsEmpty(errors);
            CollectionAssert.IsEmpty(warnings);
            var expected = SpawnCurveDefaults.Waves();
            Assert.AreEqual(expected.Count, waves.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].name, waves[i].name);
                Assert.AreEqual(expected[i].startTime, waves[i].startTime);
                Assert.AreEqual(expected[i].formation, waves[i].formation);
                Assert.AreEqual(expected[i].chestGold, waves[i].chestGold);
                Assert.AreEqual(expected[i].chestTime, waves[i].chestTime);
                Assert.AreEqual(30f, waves[i].duration);
                Assert.AreEqual(2, waves[i].pulses);
                CollectionAssert.AreEqual(expected[i].extraCount, waves[i].extraCount);
            }
        }

        [Test]
        public void ParseSegments_EnglishExport_CommaSeparatorAndDotDecimal()
        {
            const string csv =
                "Start (s),End (s),Rondadores/min,Corredores/min,Tiradores/min,Colosos/min,Refuerzo f,Nombre\n" +
                "0,60,40,0,0,0,1.25,\"Aprendizaje, parte 1\"\n";
            var errors = new List<string>();
            // Cabeceras de tiempo en inglés: no se reconocen.
            SpawnCurveCsv.ParseSegments(csv, TypeNames, errors);
            Assert.IsNotEmpty(errors);

            errors.Clear();
            var segments = SpawnCurveCsv.ParseSegments(csv.Replace("Start", "Inicio").Replace("End", "Fin"), TypeNames, errors);
            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(1.25f, segments[0].reinforcement, 1e-5f);
            Assert.AreEqual("Aprendizaje, parte 1", segments[0].name);
        }

        [Test]
        public void ParseSegments_ColumnsInAnyOrder()
        {
            const string csv = "Nombre;Colosos/min;Rondadores/min;Fin (s);Inicio (s);Tiradores/min;Corredores/min;Refuerzo f\nA;2;72;480;390;9;28;1,2\n";
            var errors = new List<string>();
            var segment = SpawnCurveCsv.ParseSegments(csv, TypeNames, errors).Single();

            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(390f, segment.startTime);
            Assert.AreEqual(480f, segment.endTime);
            CollectionAssert.AreEqual(new[] { 72f, 28f, 9f, 2f }, segment.ratePerMinute);
        }

        [Test]
        public void ParseSegments_MissingEnemyColumn_IsAnError()
        {
            const string csv = "Inicio (s);Fin (s);Rondadores/min;Corredores/min;Tiradores/min;Refuerzo f;Nombre\n0;60;40;0;0;1;A\n";
            var errors = new List<string>();
            SpawnCurveCsv.ParseSegments(csv, TypeNames, errors);

            Assert.IsTrue(errors.Exists(e => e.Contains("Coloso")), string.Join("\n", errors));
        }

        [Test]
        public void ParseSegments_BadNumber_IsAnErrorWithLineNumber()
        {
            const string csv = "Inicio (s);Fin (s);Rondadores/min;Corredores/min;Tiradores/min;Colosos/min;Refuerzo f;Nombre\n0;60;cuarenta;0;0;0;1;A\n";
            var errors = new List<string>();
            SpawnCurveCsv.ParseSegments(csv, TypeNames, errors);

            Assert.IsTrue(errors.Exists(e => e.Contains("línea 2")), string.Join("\n", errors));
        }

        [Test]
        public void ParseWaves_UnknownFormation_KeepsPreviousAndWarns()
        {
            const string csv = "Inicio (s);Rondadores extra;Corredores extra;Tiradores extra;Colosos extra;Cofre al terminar (oro);Hora del cofre (s);Nombre\n100;10;0;0;0;5;130;Oleada sorpresa\n";
            var previous = new List<WaveEvent> { new() { formation = WaveFormation.Stampede, duration = 20f, pulses = 3 } };
            var errors = new List<string>();
            var warnings = new List<string>();
            var wave = SpawnCurveCsv.ParseWaves(csv, TypeNames, previous, errors, warnings).Single();

            CollectionAssert.IsEmpty(errors);
            Assert.AreEqual(WaveFormation.Stampede, wave.formation);
            Assert.AreEqual(20f, wave.duration);
            Assert.AreEqual(3, wave.pulses);
            Assert.IsNotEmpty(warnings);
        }

        [Test]
        public void ParseWaves_FormationColumn_WinsOverName()
        {
            const string csv = "Inicio (s);Rondadores extra;Corredores extra;Tiradores extra;Colosos extra;Cofre al terminar (oro);Hora del cofre (s);Nombre;Formación\n100;10;0;0;0;5;130;Oleada 1: Cerco;Pinza\n";
            var wave = SpawnCurveCsv.ParseWaves(csv, TypeNames, null, new List<string>(), new List<string>()).Single();

            Assert.AreEqual(WaveFormation.Pincer, wave.formation);
        }
    }
}
