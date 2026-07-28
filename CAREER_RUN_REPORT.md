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

## Resultado de la validación

El flujo entero funcionó: creación, pretemporada, partidos contextuales, entrenamiento, recuperación, minijuegos, transferencias diferidas, cambios de liga, tablas, títulos y retiro. Se detectó un problema de balance: demasiados campeonatos y cero asistencias para un delantero durante 870 partidos. La prioridad siguiente debe ser calibrar probabilidades de tabla, producción por posición, lesiones, descensos y variedad de relaciones/eventos.
