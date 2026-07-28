# Camino a la Gloria — contexto principal

## Visión

Simulador single-player de carrera futbolística. La persona inicia a los 16 años, toma decisiones de alto impacto y construye un legado frente a una generación simulada de futbolistas.

## Prueba de carrera completa (27-07-2026)

- Se ejecutó una carrera completa desde creación hasta el retiro a los 40 años. Las entradas, decisiones, minijuegos, efectos y resumen se documentaron en `CAREER_RUN_REPORT.md`.
- Se corrigió la continuidad de calendario y tabla para clubes que ascienden, descienden o cambian de liga; el catálogo es una foto inicial, pero el club activo entra a su nueva división.
- Se incorporó retiro automático a los 40 años y el texto de resumen final en el estado de carrera.
- Deuda prioritaria: calibrar competencia, producción por posición, lesiones, descensos y diversidad de eventos. La prueba produjo demasiados campeonatos y cero asistencias, por lo que aún no refleja una distribución realista.
- El siguiente diseño debe conservar exactamente un minijuego manual por temporada, pero rotarlo por posición y contexto (penal, tiro libre, definición, último pase, intercepción o parada). Pretemporada y los eventos extracancha siguen siendo decisiones por clic.
- El palmarés actual solo registra campeonatos de liga por posición 1.º; faltan copa, continental, selección, premios individuales y récords con criterios de elegibilidad propios.

## Formatos de temporada que se implementarán

- La fase regular no puede entregar siempre el título. Cada liga declarará en el catálogo su fase final, ascenso/descenso y copas asociadas.
- Liga MX: torneo corto de 17 jornadas, Play-In para puestos 7.º–10.º, Liguilla desde cuartos y final; Apertura y Clausura se registrarán como torneos distintos.
- Championship: 46 jornadas, ascenso directo para 1.º–2.º y playoff de ascenso para puestos siguientes. La EFL anunció una ampliación del playoff desde 2026/27, por lo que el formato será versionado por temporada, no fijo.
- LALIGA EA SPORTS y Premier League: liga regular de ida/vuelta, descenso y copa nacional; LALIGA HYPERMOTION añade fase de ascenso después de sus 42 jornadas.
- J.League: el catálogo deberá versionar los cambios de formato de 2026 y los playoffs de transición, en vez de asumir una tabla genérica.
- Todas las copas y playoffs usarán rondas y resultados persistentes: sorteo, ida/vuelta o partido único, prórroga, penaltis, final y repercusión en títulos, dinero, fatiga, reputación y clasificación continental.

## Reglas innegociables

- Vue + TypeScript para la experiencia; ASP.NET Core 10 para todas las simulaciones.
- Sin base de datos: catálogos JSON versionados y partida persistida en IndexedDB del navegador.
- Catálogo real de referencia 2025/26–2026/27: Liga MX/Liga de Expansión MX, Premier League/EFL Championship, LALIGA EA SPORTS/LALIGA HYPERMOTION y J1/J2 League. No se incluyen escudos ni otros activos oficiales.
- Cada liga conserva cantidad de clubes, jornadas, formato, ascenso y descenso configurables.
- Carrera anual con tres a cinco eventos memorables; calendario, partidos, tabla y estadísticas se simulan completos.
- Árbol de decisiones contextual, determinista por semilla y sin repetición innecesaria.
- Todas las posiciones y reconversión con consecuencias.
- Interfaz limpia, de clics, estilo transmisión deportiva y móvil primero.
- Casino únicamente desde los 18 años, opcional, con dinero ficticio y propósito educativo; nunca dinero real.

## Estado actual

Implementado: API determinista, selección de nacionalidad/dorsal/liga/club, catálogo de clubes y competiciones, fixture persistente por liga, simulación jornada a jornada, tabla calculada con resultados, eventos anclados a partido/minuto/marcador/rival y transferencias en ventanas de mercado. El jugador tiene media por posición, atributos básicos, filigranas, pierna mala, moral, relaciones y riesgo de lesión. La interfaz muestra jornada, rival, marcador contextual y avance hasta el siguiente evento.

## Modelo obligatorio de eventos deportivos

- Ningún evento de carrera aparecerá aislado: deberá estar anclado a un partido, ventana de transferencias, corte de tabla, lesión, convocatoria o resultado de competición.
- El calendario genera todos los partidos de cada torneo. La interfaz solo presenta los encuentros y situaciones de mayor impacto, pero la tabla y estadísticas provienen del calendario completo.
- Cada partido conserva jornada, rival, localía, competición, marcador, minuto, importancia de tabla, rivalidad, condición física, rol, forma, alineación y objetivo del club.
- Los eventos de juego representan situaciones reales: ir perdiendo al minuto 70, proteger un resultado, penalti, expulsión, lesión, gol anulado, sustitución, clásico, final, descenso, ascenso, debut o convocatoria.
- Las decisiones son futbolísticas y contextualizadas: presionar o guardar energía, rematar o asociarse, arriesgar una entrada, jugar lesionado o pedir cambio, aceptar rotación, exigir minutos y negociar tras una actuación destacada.
- Cada resolución afecta variables persistentes: minutos, titularidad, forma, confianza, energía, lesión, relación con técnico/vestuario/hinchada, reputación, valor de mercado, interés de clubes, salario, estadísticas y tabla.
- Los eventos externos —prensa, agente, patrocinio, finanzas o polémica ficticia— requieren un detonante deportivo verificable: racha, clásico, lesión, traspaso, mala actuación o logro.
- El selector usa requisitos, pesos, exclusiones y enfriamiento; no repetirá la misma narrativa ni consecuencias incompatibles dentro de una temporada.
- Cada temporada programa aleatoriamente de tres a cinco eventos de partido ponderados por rival, jornada, tabla, forma, reputación y atributos. Solo el último y más decisivo activa un minijuego de precisión; los demás se resuelven con decisiones por clic.
- Cada temporada inicia con una pretemporada obligatoria: el motor propone tres atributos según posición, carencias, rendimiento, edad, lesiones y títulos; el usuario debe elegir dos. Cada mejora o caída usa una probabilidad ponderada por participación, promedio, goles, asistencias, edad, riesgo de lesión y palmarés.
- El resumen anual debe explicar la cadena causal: partido o decisión inicial, efecto inmediato, impacto en liga/copa y consecuencia para la siguiente temporada.

## Próximo trabajo

1. Añadir rivalidades, plantillas, estadios, colores y calendarios de ida/vuelta a las ligas catalogadas.
2. Implementar simulación de partidos, alineaciones, marcador/minutos y disparadores de eventos basados en contexto deportivo.
3. Persistir generación rival y ligar transferencias a rendimiento, reputación, agente, posición y necesidad de clubes.
4. Desarrollar los doce minijuegos y sus variantes por posición.
5. Añadir contratos detallados, prensa, lesiones, finanzas, premios y sistema de legado.
6. Añadir pruebas de motor, API y recorrido de navegador.

## Decisiones de interfaz y creación

- Antes de iniciar, el usuario elige nombre, nacionalidad, dorsal del 1 al 99, liga, club, posición, arquetipo y personalidad.
- La lista de clubes se filtra al elegir liga; ligas, clubes y competiciones proceden del catálogo JSON servido por la API.
- Las transferencias no son eventos aislados: una actuación en un partido importante activa scouts y ofertas; el usuario compara club, liga, salario y rol.

## Estado implementado: economia, contratos y archivo anual

- La moneda visible de carrera es EUR. Cada jugador tiene un saldo persistente, un contrato anual, salario bruto anual, deposito mensual neto, bono de firma y primas por aparicion, gol/asistencia y titulo.
- El libro mayor registra cada ingreso o egreso por temporada: nomina, primas, bonos de firma, inversiones ficticias, multas y movimientos financieros. El dinero es estrictamente interno a la partida; no existen pagos ni apuestas reales.
- Todo contrato vence al cierre de la temporada. Tras registrar el resultado anual se abre una negociacion obligatoria: renovar con el club, elegir una oferta o pedir salida al mercado. La nueva temporada inicia con contrato de un ano y pretemporada.
- La interfaz ya no requiere un boton de aceptar o avanzar tras resolver una decision. El motor sigue el calendario y encadena automaticamente el siguiente evento, el cierre anual o la negociacion contractual.
- Cada resolucion muestra el marcador final y si se gano, empato o perdio, junto con la causa narrativa y los efectos visibles de la decision sobre forma, energia, reputacion, relaciones o dinero.
- La cronologia global se complementa con un archivo expandible por temporada. Cerrado muestra solo PJ, goles, media y titulos; abierto muestra posicion final, eventos, resultados y movimientos del libro mayor.
- La partida se persiste en IndexedDB, con lectura de respaldo para partidas previas guardadas en localStorage.
- El catalogo operativo se completa a 90 eventos: los 60 eventos originales y 30 eventos de vida personal, directiva, finanzas e integridad. Los temas de casino y amaño son ficticios, solo se habilitan por edad cuando aplica y presentan consecuencias disciplinarias claras.

## Estado implementado: minijuegos funcionales

- El minijuego ya no envia una puntuacion libre. Cada desafio guarda su estado, secuencia, dificultad e instrucciones en el evento; la API valida los clics recibidos y determina exito o fallo.
- Hay diez desafios disponibles: penal, tiro libre, definicion, ultimo pase, intercepcion, parada, duelo aereo, rondo de memoria, recuperacion segura tipo buscaminas y dados de casino ficticio.
- El selector usa un unico desafio anual: 70% cancha, 20% entrenamiento y 10% ocio para mayores de 18. No se puede repetir dentro de la misma temporada.
- En un partido decisivo, el fallo del desafio fija la accion fallida y su marcador coherente; no existe una tirada posterior que convierta un fallo en victoria.

## Registro de cambios

- 2026-07-28: se implementan contratos anuales en EUR, libro mayor, archivo expandible por temporada, flujo automatico de eventos, resultados explicitos e IndexedDB; se amplia el catalogo a 90 eventos.

- 2026-07-27: se crea el documento principal y el primer vertical funcional de carrera.
- 2026-07-27: se añaden nacionalidad, dorsal, liga y equipo elegibles; las transferencias se activan mediante actuaciones en partidos clave.
- 2026-07-27: se adopta un catálogo identificable de clubes y competiciones reales, con formatos nacionales configurables. La publicación comercial requerirá revisión de licencias de marcas.
- 2026-07-27: se establece que todos los eventos y decisiones deben tener detonante de partido o competición y consecuencias persistentes sobre la temporada.
- 2026-07-27: se implementa el primer motor de calendario, tabla real por resultados y eventos deportivos contextualizados.
- 2026-07-27: se añaden decisiones de entrenamiento, recuperación y prensa, siempre disparadas por un partido, con efectos persistentes sobre atributos, relaciones, energía, riesgo de lesión y media.
- 2026-07-27: se incorpora un único minijuego de precisión por temporada para el evento decisivo; su resultado modifica la probabilidad calculada por atributos y contexto.
- 2026-07-27: se implementa la pretemporada de dos elecciones entre tres atributos, con progresión y declive conscientes del rendimiento acumulado.
