# Prueba integral de carrera — Mateo Rios

Fecha: 27-07-2026. Corrida completa contra la API local, desde crear jugador hasta retiro. Los eventos son aleatorios: este documento registra una ejecución real, no un guion fijo.

## Inputs enviados

`nombre=Mateo Rios`, `nacionalidad=Mexico`, `dorsal=10`, `liga=Liga MX`, `club=Club America`, `posición=Delantero`, `arquetipo=Talento`, `personalidad=Profesional`.

Política de prueba: elegí las primeras 2 opciones de pretemporada, `attack` en cada partido, `rest` en recuperación, la primera transferencia ofrecida y `skillScore=100` en el minijuego único de cada temporada. No se alteró el estado para obtener resultados.

Leyenda: `PRE` atributos elegidos; `✓/×` éxito/fallo de la jugada; `REC` descanso; `TR` transferencia; `GAME` minijuego decisivo. Éxito suele sumar forma/reputación/gol; fallo resta forma y energía. `REC rest` recupera 13 de energía y baja 1 de forma.

## Bitácora de todas las acciones

| Año | Acciones en orden | Cierre |
|---|---|---|
| 2026 | PRE defensa/tiro (+4/-1); J10 Monterrey ✓; REC; J11 Mazatlán ✓; entreno intenso; J12 León ×; REC; J13 Guadalajara ✓; REC; J16 San Luis GAME ✓; REC | América 1/18, 17 PJ, 4 G, 6.50; campeón Liga MX. |
| 2027 | PRE defensa/tiro (+3/-1); J3 Tigres ✓; TR Yokohama; J13 Guadalajara ✓; REC; J14 Juárez GAME ✓; REC | América 8/18, 34 PJ, 7 G, 6.87; fichaje efectivo. |
| 2028 | PRE defensa/tiro (-1/+3); J16 Nagoya ×; REC; J22 Avispa ×; REC; J31 Yokohama FC ✓; REC; J35 Nagoya ✓; REC; J36 Kyoto GAME ×; REC | Yokohama 1/20, 72 PJ, 9 G, 6.85; campeón J1. |
| 2029 | PRE defensa/tiro (+3/-1); J22 Avispa ✓; REC; J29 Fagiano ✓; REC; J30 Cerezo ✓; REC; J38 Gamba GAME ✓; REC | Yokohama 1/20, 110 PJ, 13 G, 6.65; campeón J1. |
| 2030 | PRE defensa/tiro (-1/+2); J26 Machida ×; REC; J27 Kawasaki ×; REC; J29 Fagiano ×; REC; J36 Kyoto GAME ×; REC | Yokohama 1/20, 148 PJ, 13 G, 6.78; campeón J1. |
| 2031 | PRE defensa/tiro (+3/+3); J29 Fagiano ×; REC; J37 Kashiwa ×; REC; J38 Gamba GAME ✓; REC | Yokohama 1/20, 186 PJ, 14 G, 6.73; campeón J1. |
| 2032 | PRE defensa/tiro (-1/+2); J29 Fagiano ×; REC; J30 Cerezo ×; REC; J35 Nagoya ×; REC; J38 Gamba GAME ✓; REC | Yokohama 1/20, 224 PJ, 15 G, 6.85; campeón J1. |
| 2033 | PRE defensa/tiro (+3/-1); J29 Fagiano ✓; REC; J33 Tokyo Verdy ×; REC; J35 Nagoya GAME ✓; REC | Yokohama 1/20, 262 PJ, 17 G, 6.84; campeón J1. |
| 2034 | PRE defensa/tiro (+3/+3); J31 Yokohama FC ✓; REC; J33 Tokyo Verdy ✓; REC; J38 Gamba GAME ✓; REC | Yokohama 1/20, 300 PJ, 20 G, 6.90; campeón J1. |
| 2035 | PRE defensa/tiro (+4/-1); J21 Albirex ✓; REC; J28 Kashima ✓; REC; J35 Nagoya GAME ✓; REC | Yokohama 1/20, 338 PJ, 23 G, 7.01; campeón J1. |
| 2036 | PRE defensa/tiro (+4/-1); J22 Avispa ✓; REC; J35 Nagoya ✓; REC; J36 Kyoto ✓; REC; J37 Kashiwa GAME ✓; REC | Yokohama 1/20, 376 PJ, 27 G, 7.06; campeón J1. |
| 2037 | PRE tiro/pase (-1/+4); J28 Kashima ×; REC; J30 Cerezo ✓; REC; J31 Yokohama FC GAME ✓; REC | Yokohama 1/20, 414 PJ, 29 G, 6.99; campeón J1. |
| 2038 | PRE tiro/defensa (+3/+4); J20 FC Tokyo ✓; TR Getafe; J21 Albirex ✓; REC; J28 Kashima ×; REC; J31 Yokohama FC ✓; REC; J38 Gamba GAME ✓; REC | Yokohama 1/20, 452 PJ, 33 G, 6.90; campeón J1 y fichaje efectivo. |
| 2039 | PRE tiro/defensa (+2/-1); J17 Villarreal ✓; REC; J33 Elche ✓; REC; J38 Real Madrid GAME ✓; REC | Getafe 6/20, 490 PJ, 36 G, 7.04. |
| 2040 | PRE tiro/pase (+3/-1); J28 Sociedad ✓; REC; J32 Athletic ×; REC; J37 Sevilla GAME ✓; REC | Getafe 1/20, 528 PJ, 38 G, 6.99; campeón LALIGA. |
| 2041 | PRE pase/tiro (+4/+2); J13 Athletic ✓; REC; J33 Elche ×; REC; J35 Atlético ✓; REC; J37 Sevilla GAME ✓; REC | Getafe 1/20, 566 PJ, 41 G, 7.08; campeón LALIGA. |
| 2042 | PRE defensa/tiro (+2/+3); J16 Atlético ✓; REC; J23 Barcelona ✓; REC; J26 Oviedo ×; REC; J27 Valencia ✓; REC; J28 Sociedad GAME ✓; REC | Getafe 1/20, 604 PJ, 45 G, 7.21; campeón LALIGA. |
| 2043 | PRE defensa/tiro (-1/+3); J23 Barcelona ✓; REC; J28 Sociedad ✓; REC; J32 Athletic ✓; REC; J38 Real Madrid GAME ✓; REC | Getafe 3/20, 642 PJ, 49 G, 7.17. |
| 2044 | PRE defensa/físico (+2/-2); J32 Athletic ×; REC; J33 Elche ×; REC; J38 Real Madrid GAME ✓; REC | Getafe 5/20, 680 PJ, 50 G, 7.37. |
| 2045 | PRE defensa/físico (-1/+2); J26 Oviedo ✓; REC; J29 Betis ✓; REC; J36 Villarreal ✓; REC; J37 Sevilla GAME ✓; REC | Getafe 3/20, 718 PJ, 54 G, 7.17. |
| 2046 | PRE pase/defensa (+2/+3 y desgaste ritmo/físico -1); J16 Atlético ×; REC; J36 Villarreal ×; REC; J37 Sevilla ✓; REC; J38 Real Madrid GAME ×; REC | Getafe 1/20, 756 PJ, 55 G, 7.27; campeón LALIGA. |
| 2047 | PRE defensa/físico (-2/+3); J25 Osasuna ×; REC; J28 Sociedad ✓; REC; J35 Atlético GAME ✓; REC | Getafe 1/20, 794 PJ, 57 G, 7.24; campeón LALIGA. |
| 2048 | PRE defensa/tiro (-1/+2); J31 Málaga ✓; REC; J35 Atlético ✓; REC; J36 Villarreal ×; REC; J37 Sevilla GAME ✓; REC | Getafe 1/20, 832 PJ, 60 G, 7.32; campeón LALIGA. |
| 2049 | PRE defensa/tiro (-2/-1); J30 Deportivo ×; REC; J36 Villarreal ✓; REC; J37 Sevilla GAME ✓; REC | Getafe 2/20, 870 PJ, 62 G, 7.30; retiro. |

## Resumen final

- Retiro a los 40 años en Getafe CF / LALIGA EA SPORTS.
- 870 partidos, 62 goles, 0 asistencias, promedio global 7.04, media 78 y dinero 240,490.
- 18 títulos: Liga MX 2026; J1 League 2028–2038; LALIGA EA SPORTS 2040–2042 y 2046–2048.

## Palmarés y posiciones con relevancia narrativa

| Bloque | Hecho | Por qué importa en la carrera |
|---|---|---|
| México, 2026 | 1.º de 18 con América; Liga MX 2026 | Primer título y validación de que el jugador puede rendir desde su club formador. |
| México, 2027 | 8.º de 18 con América | Salida creíble: no fue campeón ni estaba en una lucha de descenso; el rendimiento temprano activó la oferta de Yokohama. |
| Japón, 2028–2038 | 11 campeonatos J1 League consecutivos con Yokohama F. Marinos | Es el gran dominio de la carrera, pero también el principal síntoma de desbalance. El reporte debe tratarlo como hito extraordinario, no como comportamiento esperado. |
| España, 2039 | 6.º de 20 con Getafe | Primera temporada de adaptación a una liga de mayor nivel: plaza media-alta sin título. |
| España, 2040–2042 | 3 LALIGAs consecutivas | Pico de la carrera en Europa. |
| España, 2043–2045 | 3.º, 5.º y 3.º | Años competitivos sin título; son más verosímiles que el dominio continuo y deberían producir consecuencias: renovación, interés de clubes, premios y presión. |
| España, 2046–2048 | 3 LALIGAs más | Segundo pico deportivo, ya con desgaste por edad. |
| España, 2049 | 2.º de 20 y retiro | Final relevante: se retira compitiendo por el título, no tras desaparecer de la rotación. |

### Títulos registrados por el motor

| Competición | Temporadas | Total |
|---|---:|---:|
| Liga MX | 2026 | 1 |
| J1 League | 2028, 2029, 2030, 2031, 2032, 2033, 2034, 2035, 2036, 2037, 2038 | 11 |
| LALIGA EA SPORTS | 2040, 2041, 2042, 2046, 2047, 2048 | 6 |
| **Total** |  | **18** |

Nota técnica: hoy un título se concede únicamente al terminar primero la liga regular. No se simulan todavía copa nacional, supercopa, torneos continentales, selección nacional, playoffs, ascensos por promoción ni premios individuales; por tanto, esos 18 son títulos de liga, no un palmarés completo.

## Minijuegos: qué existe realmente

| Situación | Nombre mostrado | Interacción real | Frecuencia |
|---|---|---|---|
| Partido decisivo de atacante/extremo | Definición decisiva | Barra móvil de precisión: se pulsa para enviar `skillScore` de 0 a 100; el máximo aporta hasta +0.22 a la probabilidad de éxito. | Exactamente 1 por temporada. |
| Partido decisivo de defensa/portero | Última intervención | La misma barra de precisión, con narrativa defensiva. | Exactamente 1 por temporada. |
| Partido importante no decisivo, atacante | Decisión ofensiva | Dos botones: atacar o combinar. Es una decisión probabilística, no un minijuego de habilidad. | 2–4 por temporada. |
| Partido importante no decisivo, defensa/portero | Anticipación | Dos botones: presionar o sostener la línea. Es decisión probabilística. | 2–4 por temporada. |
| Pretemporada | Planificar 2 de 3 | Selección obligatoria de dos atributos entre tres; no mide destreza manual. | 1 por temporada. |
| Entrenamiento, prensa, recuperación y mercado | Entrenamiento / Conferencia / Recuperación / Negociación | Botones de decisión con consecuencias de atributos, relaciones, riesgo o transferencia. | Contextual. |

En esta corrida hubo 24 minijuegos de precisión: uno por cada temporada 2026–2049. Todos fueron `GAME=100`, por lo que la prueba validó el extremo alto de precisión; no validó el rango bajo ni la experiencia de fallar por mala pulsación.

## Mejoras priorizadas después de la prueba

1. **Balance de ligas.** Separar fuerza de club, plantilla, forma, fatiga y dificultad de liga; limitar rachas de campeonatos y hacer que el ascenso de media no convierta automáticamente al club en campeón.
2. **Resultados y estadísticas por posición.** Un delantero necesita distribución de goles, asistencias, tiros, ocasiones creadas, minutos y suplencias. Porteros y defensas necesitan porterías a cero, paradas, entradas y errores. La carrera de prueba terminó con 0 asistencias.
3. **Relevancia real de la tabla.** Antes de generar el evento decisivo, calcular escenarios exactos: puntos necesarios, rival directo, objetivo (título, Europa, salvación) y consecuencia visible en la clasificación.
4. **Variedad de minijuegos sin romper la regla de uno por temporada.** Conservar un solo minijuego manual, pero elegir entre definición, penal, tiro libre, mano a mano, último pase, intercepción, parada o salida aérea según posición y contexto. Los demás eventos siguen siendo clics.
5. **Dificultad del minijuego.** Añadir zona verde, velocidad y tolerancia variables por presión, fatiga, pie débil, atributo relevante y calidad del rival; registrar precisión, no solo éxito final.
6. **Palmarés completo.** Añadir copa nacional, competiciones continentales, selección, premios individuales y récords; cada uno debe tener calendario, elegibilidad y criterios propios.
7. **Transferencias y relaciones.** La oferta debe considerar rol prometido, salario, duración, nivel de competencia, idioma/adaptación, relación con técnico/agente y oportunidades reales de jugar.
8. **Lesiones, descenso y consecuencias.** Convertir riesgo en lesiones con duración, rehabilitación y pérdida/recuperación de atributos; modelar descenso, rescisión y presión mediática de forma persistente.

## Resultado de la validación

El flujo entero funcionó: creación, pretemporada, partidos contextuales, entrenamiento, recuperación, minijuegos, transferencias diferidas, cambios de liga, tablas, títulos y retiro. Se detectó un problema de balance: demasiados campeonatos y cero asistencias para un delantero durante 870 partidos. La prioridad siguiente debe ser calibrar probabilidades de tabla, producción por posición, lesiones, descensos y variedad de relaciones/eventos.
