# Research 来源映射与未上传清单

旧机器根目录：`E:/SteamLibrary/steamapps/common/RimWorld/Moddev/`（下称 Moddev/）。本仓库根：<repo>。

所有 dst 都是源文件的副本，内容未编辑。sha256 是复制时对**旧机源文件**计算的哈希，用于核对原件。

入库时的换行处理：51 个文件与源字节完全一致；76 个文本文件因仓库 core.autocrlf=true 按 LF 入库（与源相比只差 CRLF→LF，已逐个验证归一化后字节相同）。想要逐字节对照，请用 sha256 列核对旧机原件。

## 一、复制进仓库的文件

| 源（Moddev/…） | 目标（仓库内） | 字节 | SHA256（源文件） | 入库 |
| --- | --- | --- | --- | --- |
| `Graphify/native/metadata.json` | `Research/Graphify/native-metadata.json` | 1805 | `195AF9208CDD5A58A9E8B3CE703A38047E641842E66565C1E39811810266572B` | LF 规范化 |
| `Graphify/refresh-graph.ps1` | `Research/Graphify/refresh-graph.ps1` | 13137 | `A42713FDE20B36FB8A0E6EA4307BB2B557B96D8174CF1CE0D9F35FC49A12577B` | LF 规范化 |
| `Graphify/tools/rewrite-paths.py` | `Research/Graphify/tools/rewrite-paths.py` | 3277 | `2988F536C70DE0CD38A61694C1EB46B402FAE5CD7EB65E508B1B33EC45BF1D1C` | 字节一致 |
| `evidence/a18-experiment.jsonl` | `Research/Results/a18/experiment.jsonl` | 85537 | `0BE88E35B7EFAD755A20A13E26BB9F1493D98DE1C9C77EBE690D7CD5B1CDE3DC` | 字节一致 |
| `evidence/a18-results.jsonl` | `Research/Results/a18/results.jsonl` | 470034 | `33CCBE0C6F6D94F4D2CA488D560483EBD89179A53E0DC2AB8C1F9CEFABA6189C` | 字节一致 |
| `evidence/a19-final-experiment.jsonl` | `Research/Results/a19-final/experiment.jsonl` | 113352 | `6FE45D1544AE968528399FA29CA6C2A7DB3D42A406F0C1B832698633E5F4571E` | 字节一致 |
| `evidence/a19-final-results.jsonl` | `Research/Results/a19-final/results.jsonl` | 981310 | `E6FBD925FDAEAB189B8A31AE017E024BA23F606CFA63BC9A9AC2FC1634DE0E8D` | 字节一致 |
| `evidence/a19-soundfix-experiment.jsonl` | `Research/Results/a19-soundfix/experiment.jsonl` | 115384 | `453227FBA416EA20439BA8B27883C9C87964DE958E7104416C67431A877DD3A9` | 字节一致 |
| `evidence/a19-soundfix-results.jsonl` | `Research/Results/a19-soundfix/results.jsonl` | 999053 | `41B385308CDEE11BE06B010A3D4869C52168F8FDD89C4D3DD4EE62337FBBB480` | 字节一致 |
| `evidence/a20-moving-experiment.jsonl` | `Research/Results/a20-moving/experiment.jsonl` | 5986 | `FE9916CB91947C1FA358C6F5AF7FC14753FEBB2E3C8620DA30F7875236364321` | LF 规范化 |
| `evidence/a20-moving-results.jsonl` | `Research/Results/a20-moving/results.jsonl` | 36578 | `41CA991C8A0F7E69790C35D83D1A00331D8D726DC7C008C44E65E9DEA5080F22` | LF 规范化 |
| `evidence/a20-prelaunch/experiment.jsonl.a19` | `Research/Results/a20-prelaunch/experiment.jsonl.a19` | 130280 | `F7FAD387B11D25C08859E06D1EFC37D07ED9636CE2BB59298AD2B021D6FC2C1C` | 字节一致 |
| `evidence/a20-prelaunch/lab-status.json.a19` | `Research/Results/a20-prelaunch/lab-status.json.a19` | 251 | `4EBA69B79940EA2750FBBD5292EAD34D983BCAEEE40D1361FAC4DC6140ED57ED` | 字节一致 |
| `evidence/a20-prelaunch/observe-base.json.a19` | `Research/Results/a20-prelaunch/observe-base.json.a19` | 1964 | `793CDEDDC437E5EE52BC0D45A446DFC2303EF6D2A91A6C1034D51B3DBEA61E88` | LF 规范化 |
| `evidence/a20-experiment.jsonl` | `Research/Results/a20/experiment.jsonl` | 135668 | `8618BE494F182FDD499DE509CA738F8620E484446AA8ECB62FC282C66349C335` | 字节一致 |
| `evidence/a20-results.jsonl` | `Research/Results/a20/results.jsonl` | 1042246 | `ADCBDDBEBB83D752B330C4DB0303426631371A6C9F939B8E0027301DDAA1C59F` | 字节一致 |
| `lab-run/experiment.jsonl` | `Research/Results/lab-run/experiment.jsonl` | 148583 | `C93FD5F1BD9EC41CD818BF4C4FFEC6C66EC4ED0A7F692FFF5F4862CC34742F1D` | 字节一致 |
| `lab-run/lab-status.json` | `Research/Results/lab-run/lab-status.json` | 251 | `FF323CC5EE8CFA01F3285484D55A2817A8D50B22A9863E39A7E3F646F17FAA5B` | 字节一致 |
| `lab-run/observe-base.json` | `Research/Results/lab-run/observe-base.json` | 1964 | `793CDEDDC437E5EE52BC0D45A446DFC2303EF6D2A91A6C1034D51B3DBEA61E88` | LF 规范化 |
| `lab-run/requests.txt` | `Research/Results/lab-run/requests.txt` | 41768 | `EBFBE508DEA8EB006D37CA45A53CCDD010D57E90E08485638C2C4657E356A43F` | LF 规范化 |
| `lab-run/results.jsonl` | `Research/Results/lab-run/results.jsonl` | 1080769 | `EE6D43F7D34DC65F52784F1CB10F23519309908E0CA1B140200B0CC4AA3D805D` | 字节一致 |
| `evidence/first-flight.mp4` | `Research/Results/Media/first-flight.mp4` | 546586 | `64C3AA5C14DCE388B2832AD83F637AE673F9A7F4158B2B5F34618AC58971004A` | 字节一致 |
| `tools/check-elf/check_elf.ps1` | `Research/Tools/check-elf/check_elf.ps1` | 6198 | `E6EAFF9445CA58E500C2DD0D1E48C3DB7E75686664E7BC18C4087558BAA1294A` | 字节一致 |
| `tools/check-elf/check_injections.ps1` | `Research/Tools/check-elf/check_injections.ps1` | 6220 | `3A70B1C69AF3B487CD07E2345D93746EFA0BAEFCC1C54F9A0AA41D34346E6FCA` | 字节一致 |
| `tools/check-elf/find.ps1` | `Research/Tools/check-elf/find.ps1` | 960 | `398FD7BFBA523CB547BFEA288CF73A3F77E0B3D5CF33DE994533DED6430379D4` | 字节一致 |
| `tools/check-elf/il.ps1` | `Research/Tools/check-elf/il.ps1` | 1645 | `3DF665837B8E6375146B4A5EA965B7E774DB4046BEB3024F50148E38BDDF09B3` | 字节一致 |
| `tools/check-missiles/callsites.ps1` | `Research/Tools/check-missiles/callsites.ps1` | 861 | `E76DB37D18194E848FE8B415147FC00A087A9D2EF942B6C1CFC205D87657864A` | 字节一致 |
| `tools/check-missiles/check_defs.py` | `Research/Tools/check-missiles/check_defs.py` | 3814 | `48A106D311B40EB64E6ACAB7CDB97CACEC8AD8368FE5414FD2B0679BC983C40C` | 字节一致 |
| `tools/check-missiles/check_lang.py` | `Research/Tools/check-missiles/check_lang.py` | 2946 | `62796354E7D75A31CD3EFB39C76076DBB5D88FFAEA3E7E7B69423FF97B239694` | 字节一致 |
| `tools/check-missiles/check_refactor.py` | `Research/Tools/check-missiles/check_refactor.py` | 8537 | `3BC602C8D9AC3A2C42510E4676EFAABB4325C91D0A4D78CF56F3EA8E11C4223C` | 字节一致 |
| `tools/check-missiles/dump.ps1` | `Research/Tools/check-missiles/dump.ps1` | 2952 | `2192BAB838557618736B2345B85B37DA6C67865E98CFD5F971D772DB136D9525` | 字节一致 |
| `tools/check-missiles/ExperimentLogStub.cs` | `Research/Tools/check-missiles/ExperimentLogStub.cs` | 512 | `482B27C2B5009B0AC0A5E3C0AD277D3CBC238AAE1014EE83275D2AA81B2D219E` | 字节一致 |
| `tools/check-missiles/il.ps1` | `Research/Tools/check-missiles/il.ps1` | 1620 | `07CC31598F564FB0A7E3D7578A82EAB987F3F62144420236723EE4878DA890D5` | 字节一致 |
| `tools/check-missiles/make_icon.py` | `Research/Tools/check-missiles/make_icon.py` | 1847 | `E4BD94A287C4137052716D450D3CA2A4D8038D339E2A67D0C6641D5A99A491E0` | 字节一致 |
| `tools/check-missiles/motion-harness/analysis/AnalysisProgram.cs` | `Research/Tools/check-missiles/motion-harness/analysis/AnalysisProgram.cs` | 14283 | `61D93F5B009350D01B8F620998FA6E93773D615A7E93432F035386E19A600E91` | 字节一致 |
| `tools/check-missiles/motion-harness/analysis/WotF_MissileAnalysis.csproj` | `Research/Tools/check-missiles/motion-harness/analysis/WotF_MissileAnalysis.csproj` | 1831 | `AE3598B2D383D0AA3762B4051BDE5C120BFAFC586C9B4FE1B4412E5C39D349D9` | 字节一致 |
| `tools/check-missiles/motion-harness/Program.cs` | `Research/Tools/check-missiles/motion-harness/Program.cs` | 47366 | `DB6FDB23CD40AD8A0712E2994F75C318EF9E97E702DCA8D01F6B4B87AF89F03D` | LF 规范化 |
| `tools/check-missiles/motion-harness/Shims.cs` | `Research/Tools/check-missiles/motion-harness/Shims.cs` | 14996 | `A07DD139C52C24F1A718E7E4B0C7B0DEBFCB4C97CF0F8B044A91FD7ED179A8CE` | LF 规范化 |
| `tools/check-missiles/motion-harness/WotF_MissileMotionHarness.csproj` | `Research/Tools/check-missiles/motion-harness/WotF_MissileMotionHarness.csproj` | 2433 | `41D8AC8CAFC0BBB0A2BB7AA66011D87D88C9FC8BC32849752261F2D00B5699E8` | 字节一致 |
| `tools/check-missiles/WotF_MissilesCheck.csproj` | `Research/Tools/check-missiles/WotF_MissilesCheck.csproj` | 2430 | `29F8F1A24845D3757797E5988F661430A22E0C1464811947BD4B3AA371C450AC` | 字节一致 |
| `tools/check-movement/check-integration.csproj` | `Research/Tools/check-movement/check-integration.csproj` | 2714 | `212B1D03B8412C54DB527234AB4D1F8F130C1D418B9180659D1F3BFE6710853E` | 字节一致 |
| `tools/check-movement/check-movement.csproj` | `Research/Tools/check-movement/check-movement.csproj` | 1498 | `059FD93A955BB319C5E62F642F71B0DE4E8C69FA559CA7FADC2F9E86B48A9DE9` | 字节一致 |
| `tools/check-movement/il/check-deployed-version.ps1` | `Research/Tools/check-movement/il/check-deployed-version.ps1` | 527 | `BE910991797E5A3DD9CDF146F62B4FAD66FA27DC3EF7A4CAA44FB805281ADB01` | LF 规范化 |
| `tools/check-movement/il/dump-il.ps1` | `Research/Tools/check-movement/il/dump-il.ps1` | 1497 | `EA55B745EA14ED1CCC606BDF11B04D8B6900A908790EA72634A7719437A01ACE` | 字节一致 |
| `tools/check-movement/il/find-apparel.ps1` | `Research/Tools/check-movement/il/find-apparel.ps1` | 352 | `8FAC7F18D74C9C9B420B6F89CED66087B92768D603B34AC449C53AAB7C77A455` | LF 规范化 |
| `tools/check-movement/il/find-definject.ps1` | `Research/Tools/check-movement/il/find-definject.ps1` | 495 | `5E64AA9E04B7ECB9709E04026F354595861D76B58F52D65557902810807EB372` | LF 规范化 |
| `tools/check-movement/il/find-smelt.ps1` | `Research/Tools/check-movement/il/find-smelt.ps1` | 585 | `5234E35C8CA40254308D617E78203378F59D16F163F74A67B9FEF6B8E1F9FAD7` | LF 规范化 |
| `tools/check-movement/il/find-translate.ps1` | `Research/Tools/check-movement/il/find-translate.ps1` | 354 | `DAA814E08D8B489299EA09A97D0AFEC093E041F752A00F96E383E33437339FA6` | LF 规范化 |
| `tools/check-movement/il/find-translate2.ps1` | `Research/Tools/check-movement/il/find-translate2.ps1` | 402 | `D88179A115851DB72BB8ADB7F1DB04F09E4E19C1C4B67AF94A1DE4140B2A988B` | LF 规范化 |
| `tools/check-movement/il/find-worngraphicdata.ps1` | `Research/Tools/check-movement/il/find-worngraphicdata.ps1` | 635 | `BE3E634A925088C7E8C84FA26CDD2625AED382FD96F70F7EF69C9566309F5D12` | LF 规范化 |
| `tools/check-movement/il/list-assets.ps1` | `Research/Tools/check-movement/il/list-assets.ps1` | 358 | `5E00426B1C8DBA9C8D18D9BFD74A9A116E756840CD2F7DB9D565537415B00B91` | LF 规范化 |
| `tools/check-movement/il/list-fields.ps1` | `Research/Tools/check-movement/il/list-fields.ps1` | 746 | `E6CD8FF84FAFB53A71CDFFC8D95947AEAFA2A095B14345FE94AD5C6088811364` | 字节一致 |
| `tools/check-movement/il/list-methods.ps1` | `Research/Tools/check-movement/il/list-methods.ps1` | 839 | `8FE0F7DE38F1B0F65AD8A5F5483C286F61F16B1BEB16762A7D2874FBFAA0096E` | 字节一致 |
| `tools/check-movement/il/list-methods2.ps1` | `Research/Tools/check-movement/il/list-methods2.ps1` | 849 | `1E8780523C50CAD0CAAB067D89B9FA229CB7C8693A57AB67114F6CC18FDC528F` | 字节一致 |
| `tools/check-movement/il/make-apparel-textures.ps1` | `Research/Tools/check-movement/il/make-apparel-textures.ps1` | 2048 | `2ABBF404CAB2C811A1206E41E24C0D8548FC483C3FC54438BF7B0B6A041FB904` | 字节一致 |
| `tools/check-movement/il/scan-callers.ps1` | `Research/Tools/check-movement/il/scan-callers.ps1` | 847 | `CBEA937EE095F78C64A1008EFAE514A89E2D7012D59E7F3899B645B5F891C421` | 字节一致 |
| `tools/check-movement/il/scan-equip.ps1` | `Research/Tools/check-movement/il/scan-equip.ps1` | 848 | `4B9697C4F71D69823070CF69EB2F2F4B848C9D69E36E52B38916F0E8D967FF81` | 字节一致 |
| `tools/check-movement/list-deployed.ps1` | `Research/Tools/check-movement/list-deployed.ps1` | 584 | `FBE0E5E19BE4C5D8F31EA2CC236CCCDA275739FC3DF678F7E410C22073E01B1E` | LF 规范化 |
| `tools/check-movement/list-types.ps1` | `Research/Tools/check-movement/list-types.ps1` | 440 | `4C7E7ABDB27B55E5AAB18FA508EE2828336218E8491869E10FCD918A4363CA83` | LF 规范化 |
| `tools/check-movement/Stubs/ExperimentLogStub.cs` | `Research/Tools/check-movement/Stubs/ExperimentLogStub.cs` | 406 | `2685479FF1F7AF84FF800926C4752BC592B380D306662598156753398EB193EB` | 字节一致 |
| `tools/check-movement/verify-patches.ps1` | `Research/Tools/check-movement/verify-patches.ps1` | 2125 | `35F6C1C0A74CC3AC67DFF606BE612781CCB135C55ABDD1893942B24AA3B1FE96` | 字节一致 |
| `tools/check-movement/verify-worn-textures.ps1` | `Research/Tools/check-movement/verify-worn-textures.ps1` | 2776 | `C26C2E9D212FB303717BBDC95D155E6393D0EB9D5BCD0794F4334DE82F2FCAC7` | 字节一致 |
| `tools/check-ui/alldirs.ps1` | `Research/Tools/check-ui/alldirs.ps1` | 460 | `A2AC0F153F862817382C4583EF92DFAF874B321F8D76760F835F8A02B3245473` | LF 规范化 |
| `tools/check-ui/alldirs2.ps1` | `Research/Tools/check-ui/alldirs2.ps1` | 547 | `1F9B72BC6D28FCD320E1F178118160A4827363B2D6309B019056906BD0920F63` | LF 规范化 |
| `tools/check-ui/api1.ps1` | `Research/Tools/check-ui/api1.ps1` | 877 | `4EBA838A1D632EDE63BAAFEEFB3DF02CDC85C2706E20F3E20B88ACDDA2E18B2C` | LF 规范化 |
| `tools/check-ui/api2.ps1` | `Research/Tools/check-ui/api2.ps1` | 1169 | `5AAD91177A285FC896DF55068315DA0B48011B474F31C9B731F3EA25EAD2A929` | LF 规范化 |
| `tools/check-ui/bck.ps1` | `Research/Tools/check-ui/bck.ps1` | 464 | `6602C6BCA118DF9C610C65F35FB5EB73DBF51AA0B4D16BD72614728B142F731B` | LF 规范化 |
| `tools/check-ui/buttonimage.ps1` | `Research/Tools/check-ui/buttonimage.ps1` | 511 | `1BF8EE11A93E8E14E2ED7AFE93AA245251126F4B429F46C5CCAF9E5B1E15E1F8` | LF 规范化 |
| `tools/check-ui/callers.ps1` | `Research/Tools/check-ui/callers.ps1` | 853 | `969B8801B06E39794AED8A177F6F28A90DB101954B7CB4989B795F5B4F3D9B4F` | LF 规范化 |
| `tools/check-ui/check-ui.ps1` | `Research/Tools/check-ui/check-ui.ps1` | 6672 | `B465089C9CA9BA163AD534B4DD1C70AE114211BF4C3AB7BE0E7EBA59B1E96B2A` | 字节一致 |
| `tools/check-ui/clickconsume.ps1` | `Research/Tools/check-ui/clickconsume.ps1` | 780 | `7D3F385018EF3807E419BEC98491581E5AE97CD215BC9673CD41F5F8C09B1D4B` | LF 规范化 |
| `tools/check-ui/cmc.ps1` | `Research/Tools/check-ui/cmc.ps1` | 455 | `022FF7B564A152B96C559383496288A0E08BD7E7B5CF7667BBBA8744100C2E65` | LF 规范化 |
| `tools/check-ui/defaults.ps1` | `Research/Tools/check-ui/defaults.ps1` | 765 | `B087097DCCCB4B732946CA5D5B6BD21FD806A8186394C207B4BA2B7E39269A5B` | LF 规范化 |
| `tools/check-ui/deffields.ps1` | `Research/Tools/check-ui/deffields.ps1` | 592 | `E9578D30BA1B2F5174D6623AC3C958F9889FEA336BDF9CBAF643658B22888BA1` | LF 规范化 |
| `tools/check-ui/dip.ps1` | `Research/Tools/check-ui/dip.ps1` | 479 | `6936AF9D9B2E8983915CD7CC704A7736C3E61396659E53D9323C3C549232E4A0` | LF 规范化 |
| `tools/check-ui/dipstrings.ps1` | `Research/Tools/check-ui/dipstrings.ps1` | 523 | `69D07C5C7D69C682A9A95B330CEC1C667101900B61407087E0EB40C3063E437C` | LF 规范化 |
| `tools/check-ui/dtf.ps1` | `Research/Tools/check-ui/dtf.ps1` | 583 | `597D82AE8265BC386FCAEFBA3E45EB189147312F7979B69EE05D6A5529699F91` | LF 规范化 |
| `tools/check-ui/dtf2.ps1` | `Research/Tools/check-ui/dtf2.ps1` | 533 | `CFD1FF0B07DC9BF93B8763FD746416D95E09FE185027F39B0D8D8EF335329A0D` | LF 规范化 |
| `tools/check-ui/dtf3.ps1` | `Research/Tools/check-ui/dtf3.ps1` | 499 | `3BD0AA2661B4E21A98B2F4C0EFF7B9DECB98E51C4DAA3B796C53F7CD6B83FE14` | LF 规范化 |
| `tools/check-ui/dtf4.ps1` | `Research/Tools/check-ui/dtf4.ps1` | 510 | `06ECFF640ED1DFB6241CD92E89C35C7C43937EA6E8BA7D8E3438CA99B104D564` | LF 规范化 |
| `tools/check-ui/ext.ps1` | `Research/Tools/check-ui/ext.ps1` | 571 | `37702CAA8898C11EC12334BFB1FEEF2865F4B13609A96178AA88EF23B6F9157C` | LF 规范化 |
| `tools/check-ui/folders.ps1` | `Research/Tools/check-ui/folders.ps1` | 790 | `53B9EAE705F1DBABDB8A9E10ADF7058E4C9D621F9E577C3343D79DFB347819BE` | LF 规范化 |
| `tools/check-ui/game.ps1` | `Research/Tools/check-ui/game.ps1` | 616 | `E8B83303ABA8BC3F40677E418B641F2787ECF5671F482878B22DE68D14D55B93` | LF 规范化 |
| `tools/check-ui/game2.ps1` | `Research/Tools/check-ui/game2.ps1` | 579 | `13AD1B1C22A4B42AE0D7BBE371B299A1587E2C836D27E56BDF5BD391F3AE0C7F` | LF 规范化 |
| `tools/check-ui/gc.ps1` | `Research/Tools/check-ui/gc.ps1` | 459 | `DAAB60210994B2BCB73A16D295B0E8B4AB4D10914F8E5DE90D72EDB8F611EE11` | LF 规范化 |
| `tools/check-ui/gcctor.ps1` | `Research/Tools/check-ui/gcctor.ps1` | 964 | `4F611E304165F90655E9C8798F476F3D8065DD5D823D0AF563B82B3BF4F5F7BE` | LF 规范化 |
| `tools/check-ui/gcu.ps1` | `Research/Tools/check-ui/gcu.ps1` | 694 | `D7CAF8C7FE1D5DDA126C17616C10A1BDDC3BDFD82E15451C71500014AA76073B` | LF 规范化 |
| `tools/check-ui/gcu2.ps1` | `Research/Tools/check-ui/gcu2.ps1` | 1066 | `9578C1990CBC213A1983641F70BF74FC6F62F149B4E8FD08395D05B1E8D51248` | LF 规范化 |
| `tools/check-ui/highlight.ps1` | `Research/Tools/check-ui/highlight.ps1` | 600 | `BB321F41BDFEA6F178B2559CC7B75AF1CD6EE9D52CF207FB73914DA82175CCB0` | LF 规范化 |
| `tools/check-ui/inject.ps1` | `Research/Tools/check-ui/inject.ps1` | 845 | `98783023075BFA042FFE8D74EF0B5352275B98A55CCF7D64F859EC9F84CA335B` | LF 规范化 |
| `tools/check-ui/lang.ps1` | `Research/Tools/check-ui/lang.ps1` | 712 | `A819922DF12F2727CFB0E67B8875C24AE29A6B72EE69E79DE453A12A8037F4F3` | LF 规范化 |
| `tools/check-ui/lerr.ps1` | `Research/Tools/check-ui/lerr.ps1` | 1001 | `9D00B7B12E8FD9010148EAACD17904EF1C1468179CF42E019FAB4EBE35B9FC9E` | LF 规范化 |
| `tools/check-ui/ll.ps1` | `Research/Tools/check-ui/ll.ps1` | 450 | `C9F80B777DFDC55DF41644F4F2A9B81A4C7BA4275662A178BB0F183801076F4D` | LF 规范化 |
| `tools/check-ui/lmm.ps1` | `Research/Tools/check-ui/lmm.ps1` | 572 | `35D1C30539A32EF0E7FCA221C58C177565D1A35FB7AE12BEB415B0C446858977` | LF 规范化 |
| `tools/check-ui/mainbuttons.ps1` | `Research/Tools/check-ui/mainbuttons.ps1` | 529 | `9EBC1DFBC69E65CE070CB404B305D89195A2F031313742F2AC6C5F5AE2C9551A` | LF 规范化 |
| `tools/check-ui/make-icon.ps1` | `Research/Tools/check-ui/make-icon.ps1` | 2356 | `87B0F6D125F17AEE5CDC91694C943E500554D8CEDADA9793A3608662F4B81FB7` | 字节一致 |
| `tools/check-ui/mb.ps1` | `Research/Tools/check-ui/mb.ps1` | 715 | `512E8457551014EA212A6B957644CDAFE55EF837A83526EF94D3E7AC4620970A` | LF 规范化 |
| `tools/check-ui/mb2.ps1` | `Research/Tools/check-ui/mb2.ps1` | 708 | `40233B6D1C6DA0E4CEBBDA470F5C9FB76C6FD74AF1F778453413CF7AA1BA6C5B` | LF 规范化 |
| `tools/check-ui/mb3.ps1` | `Research/Tools/check-ui/mb3.ps1` | 576 | `1613DB88E8371C91B3CD13DC7BCF78DF0374EE548444D7A41BA0287065BC5FF0` | LF 规范化 |
| `tools/check-ui/misc.ps1` | `Research/Tools/check-ui/misc.ps1` | 622 | `DE6283CA3C6221B4E743994199D68A4E40A282730DD203AA8C89D362F38C8CC6` | LF 规范化 |
| `tools/check-ui/mouse.ps1` | `Research/Tools/check-ui/mouse.ps1` | 935 | `5D5A636E406C94F73A3F428050EAF6ED9540754F1B2057DB169C59CA8F76D6DF` | LF 规范化 |
| `tools/check-ui/probe.ps1` | `Research/Tools/check-ui/probe.ps1` | 1492 | `8347B0362F567544F3BD88573B83A40D5F1BD52D426AB91953B5E8A3A4533F4B` | LF 规范化 |
| `tools/check-ui/probe2.ps1` | `Research/Tools/check-ui/probe2.ps1` | 1125 | `32350A962D1CFB4806876C7A40C503AFBC9DA3F529892CABA8E0B0BAB014F6B2` | LF 规范化 |
| `tools/check-ui/probe3.ps1` | `Research/Tools/check-ui/probe3.ps1` | 448 | `93821D611360C090805DE93841C4E7244D76FD0E4C2141AD31C5467A26A1FAEE` | LF 规范化 |
| `tools/check-ui/proj-real/UI-check-real.csproj` | `Research/Tools/check-ui/proj-real/UI-check-real.csproj` | 1783 | `9F56DD791BED0E25F3B84589B663AE82959F60F515BE2B58F318408C4479DDF6` | 字节一致 |
| `tools/check-ui/proj/Stubs/LabActionsStub.cs` | `Research/Tools/check-ui/proj/Stubs/LabActionsStub.cs` | 560 | `2DC1E02EFD4497C29CBA5E29A3AEBAB0F78C9F31F855D843D8E0A08C97EADCC9` | 字节一致 |
| `tools/check-ui/proj/UI-check.csproj` | `Research/Tools/check-ui/proj/UI-check.csproj` | 1522 | `2C9361DA18C1DD0555438207B71EEE259AD5739BF223C0A81BEFF3F04C336698` | 字节一致 |
| `tools/check-ui/refs.ps1` | `Research/Tools/check-ui/refs.ps1` | 975 | `F62F1A6BAE73B26DCD47C045707FC1E9130164CF886E6DAD0CC4E965E24DE05B` | LF 规范化 |
| `tools/check-ui/rowentry.ps1` | `Research/Tools/check-ui/rowentry.ps1` | 1128 | `48F031B0D7328033403F02EB1057A78AE65BED81DC9885A513EDC444DE74E86A` | LF 规范化 |
| `tools/check-ui/scan-definject-en.ps1` | `Research/Tools/check-ui/scan-definject-en.ps1` | 2524 | `CA76850D969324276B27635B73506B1D58954CAEF89FC079031293425AC99DE7` | 字节一致 |
| `tools/check-ui/scan-lang.ps1` | `Research/Tools/check-ui/scan-lang.ps1` | 2664 | `64D56F7D3E8685D0DF30337ADDEC6B4B2F9BA591F046967A8BE1D31F6D6420AC` | 字节一致 |
| `tools/check-ui/scos.ps1` | `Research/Tools/check-ui/scos.ps1` | 536 | `85833919637D678E4B88D802634281DB979AEE3A55FD0A44067442481BF34D71` | LF 规范化 |
| `tools/check-ui/scos2.ps1` | `Research/Tools/check-ui/scos2.ps1` | 492 | `FF032C8710AF309C670F02D7F13005905F876F6EF0EC3FC5E9131B75BD92B373` | LF 规范化 |
| `tools/check-ui/scos3.ps1` | `Research/Tools/check-ui/scos3.ps1` | 519 | `EB4EF956F3D103F1CDBEEEC48D0A46F8D7C1E48E3FE97EE040FC5DE0E792F261` | LF 规范化 |
| `tools/check-ui/sdfap.ps1` | `Research/Tools/check-ui/sdfap.ps1` | 490 | `FCF29975308A83208BD846A45B6A07433A50C8FA272356F634C13CE4F00A58BC` | LF 规范化 |
| `tools/check-ui/tabs.ps1` | `Research/Tools/check-ui/tabs.ps1` | 531 | `0040EFC81928BBD265E7BC52C754648C75D3AE520346CDCB790E4402A96BE3DB` | LF 规范化 |
| `tools/check-ui/tai.ps1` | `Research/Tools/check-ui/tai.ps1` | 497 | `B52912929EE138C38C33A675BC3E31E75FE76F60455B6AAF577C2BCB6786492D` | LF 规范化 |
| `tools/check-ui/translator.ps1` | `Research/Tools/check-ui/translator.ps1` | 879 | `24B41422B7558E5770253101F9114765CC6FF199AB18521DB3D1EAEFC3247995` | LF 规范化 |
| `tools/check-ui/types.ps1` | `Research/Tools/check-ui/types.ps1` | 366 | `8905BAADFA5AF02BA2861644BC056C6598B5E547632380CC7382360474B0A180` | LF 规范化 |
| `tools/check-ui/uiroot.ps1` | `Research/Tools/check-ui/uiroot.ps1` | 485 | `5F2B4B4FCA45D53F5B02476998232015560D5BD559D4A08B40504B1CF7E5675A` | LF 规范化 |
| `tools/check-ui/verify-fix.ps1` | `Research/Tools/check-ui/verify-fix.ps1` | 754 | `1FA51001BA31483C5F59C957C16E05B2E7C1D0E90DEE5ED7AA582130E7D5BBCC` | LF 规范化 |
| `tools/check-ui/widgetrow.ps1` | `Research/Tools/check-ui/widgetrow.ps1` | 773 | `A98D6AB0AF157DAD8E5E66998FF1ACB9B29A377A3D56F61B1A5FD2B9EC2178D5` | LF 规范化 |
| `tools/check-ui/winctor.ps1` | `Research/Tools/check-ui/winctor.ps1` | 766 | `9465AA38FD6D5C9636BFBE5F0921C965A2D2A06B5C7F892DF12C4C844905054B` | LF 规范化 |
| `tools/check-ui/window_il.ps1` | `Research/Tools/check-ui/window_il.ps1` | 597 | `26CD2B59397402F041B57C52A58108BFB8D1B32EB570EBCDA9C06B2D4403018D` | LF 规范化 |
| `tools/check-ui/wordwrap.ps1` | `Research/Tools/check-ui/wordwrap.ps1` | 840 | `14A70F26E5524F5F1AEFCAB145D69FAB212023ECE9BA5DD31F544D05578537D3` | LF 规范化 |
| `tools/check-ui/write.ps1` | `Research/Tools/check-ui/write.ps1` | 439 | `0A2A742781C90D07D87A6BED638FF0CCC1309986940D16EAABE89A79B99CDB0A` | LF 规范化 |
| `tools/evidence/driftcheck/DriftCheck.csproj` | `Research/Tools/evidence-driftcheck/DriftCheck.csproj` | 2642 | `B75332B75CAE2ACA93923BB09ACF48802A815CD647893415A44231E7A8DBCFA6` | 字节一致 |

合计 **127** 个复制文件、**6158618** 字节（≈ 5.9 MB）。

## 二、本仓库新写（无旧机原件）

| 文件 | 生成方式 |
| --- | --- |
| `Research/README.md` | 手写：目录说明 |
| `Research/HANDOFF.md` | 手写：交接主文档 |
| `Research/SOURCES.md` | 由复制清单与未上传清单生成 |
| `Research/Tools/README.md` | 手写：工具用途与迁移常量 |
| `Research/Results/README.md` | 手写：结果格式与读法 |
| `Research/Graphify/NOTES.md` | 手写：图谱重建说明 |
| `Research/Results/Logs/a18-acceptance-excerpt.md` | 从 `Moddev/evidence/a18-acceptance-player.log` 抽取（源字节数/SHA256 见文件头），本地用户路径已脱敏 |
| `Research/Results/Logs/a19-soundfix-excerpt.md` | 同上，源 `Moddev/evidence/a19-soundfix-player.log` |
| `Research/Results/Logs/a20-excerpt.md` | 同上，源 `Moddev/evidence/a20-player.log` |

## 三、明确未上传的内容

| 旧机路径（Moddev/…） | 字节 | SHA256 | 原因 |
| --- | --- | --- | --- |
| `lab-profile/Saves/a18-door.rws` | 12775972 | `2FDB47EB54459E77019C711832B6DF4A23E0ACBC5B984844E5F38E7A5F3B12DD` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a18-final.rws` | 12783682 | `1F0EE13058E7332833BFA8E878CFF016ED4F874C8BEA18512E030AFD5AF5C313` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a18-final.rws.bak` | 12776328 | `43A00AC057BD8B46DCD5766EF99C02CCF0CA96B82D25F6DCFAA1AF0389B6A717` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a18-flight.rws` | 12973922 | `CC850D9F0CDFF2E1BCA6D958843386ACD8702F73F29CE343D451196E15E2B4AB` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a18-ui.rws` | 12774461 | `771676A6B4E4688E993C7342F5419DE98DDF28D8E408D92C2D27A05AB018D806` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a19-base.rws` | 12783776 | `332E1E9A6E5912AE46BB0EF7854287033751DFFAE93E36859364FD44ACF6D6BA` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a19-final.rws` | 12760089 | `C93DE88320B6899ED305523F8CEDBE856394128C852A0534483A50ECF40D7AFA` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a19-mender-flight.rws` | 12760761 | `49B53FB1B66037C95D8A09D81AAA7EB833B29685D78C693442335D7A19A84093` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a19-preui.rws` | 12784775 | `A34351793BB55665EF0E0C02B407284AC6CE286485660B883FA4D8BD2DCAC2B9` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a19-ui.rws` | 12784755 | `AE5481750F0112BAABF6D0554DCF32084D51C2CDE0273C00BA17491FBC757860` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a20-final.rws` | 12756887 | `FD3A8021587352F6071C12D7F686E66EFD1BBE97055724309FF5A49010DFD3FD` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a20-flight.rws` | 12764121 | `C350DDFB77E9317A003A541347CAA2857684D10E61A481F79650EFE5A3F48207` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/a20-moving-flight.rws` | 13497066 | `E7BC63E78C55D6A05398CDEA0AE72A1C55295AFBAE51ED3F4C43C65EE186FEF6` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/wotf-a17.rws` | 22243441 | `AB048EB0ADEA8C67B06B390D5A8C51C32D8F4E146E0297D5984CB1603C6B76CA` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `lab-profile/Saves/wotf-lab-initial.rws` | 6839189 | `BAA88BCEA628ECBE64D4A2D87E93D1F5C4F0AD075310486703A799A77BF87C4D` | savegame stays machine-local; rebuild an equivalent scene per HANDOFF §8.4 (old scene cannot be restored) |
| `evidence/baseline-main-menu.png` | 4522950 | `53EC7D3B35F3A9BDFB1598F485F8CDA1D2B075024F71258E1C3BF338B9A6A89F` | 4.5MB screenshot, regenerable |
| `evidence/a18-acceptance-player.log` | 53545 | `8DAB34512EA28FC83EDD5C13F33914AFFF01A78AF1EC741957129838A6EFDF11` | full Player.log (56KB); only sanitized excerpts shipped |
| `evidence/a19-final-player.log` | 59374 | `7760706F30323EB5A33FE24714AB927CEADDBEC22D2FD9ED66BDA96E698EC823` | full Player.log |
| `evidence/a19-precut-player.log` | 59374 | `7760706F30323EB5A33FE24714AB927CEADDBEC22D2FD9ED66BDA96E698EC823` | full Player.log |
| `evidence/a19-prelaunch-player.log` | 471439 | `6C2FE2E965DA7AE6CF1C55BCE9FD12DA237F8CCFE75E972FD8C5321581DDF1FD` | full Player.log (471KB) |
| `evidence/a19-run1-harmonyfail-player.log` | 61316 | `E8779B3F95BF11591C6E4E7CD4433FED637ABFD30BC353A4394C78A1D5A656F2` | full Player.log |
| `evidence/a19-soundfix-player.log` | 52520 | `8D39376D43DF2FA9475C99990F6353B66787CB2C330A0979E62B1E5F339CDC7F` | full Player.log |
| `evidence/a20-player.log` | 112812 | `CC55B088F2D85BBF5B8E34C10AEAEA763E93E571922B1F259FC99A38996C71A6` | full Player.log (112KB); only sanitized excerpts shipped |
| `evidence/baseline-player.log` | 46157 | `46C1DBF3B9E318A868F60DEA6522406718CBB898FBE66F107D3E360085131EA5` | full Player.log |
| `evidence/first-run-player.log` | 75368 | `CECA8D73520C8A01BE4E383B9D6D63F9400E29EB2442BE70B3660C8685E0FB7A` | full Player.log |
| `evidence/second-run-player.log` | 54468 | `6E5E852D8502EC6F434C003A681C900BFC85C2F60515F5E595C08FDF9570A6B8` | full Player.log |
| `evidence/third-run-player.log` | 301903 | `849FFB03927DCC9C53CA0FD10E4FB56A0E77A9A8D32E14CE77D1344067EFB2CC` | full Player.log (301KB) |
| `evidence/fourth-run-player.log` | 56492 | `9943547FBFC686FE349A16413123D044C05EC64126BDABFE05EF09FD862E887C` | full Player.log |
| `evidence/a20-prelaunch/results.jsonl.a19` | 999053 | `41B385308CDEE11BE06B010A3D4869C52168F8FDD89C4D3DD4EE62337FBBB480` | byte-identical duplicate of a19-soundfix-results.jsonl |
| `evidence/a20-prelaunch/requests.txt.a19` | 37941 | `B99BAC689DE02727795835F9E8CF409002DB9142552541779A798FF14486DC01` | duplicate copy of lab-run requests snapshot |
| `Graphify/native/source/Verse.LocalTargetInfo.cs` | 4462 | `33E2384162EC08865DF636EACD5A276685150EDFBDB4BD290BCF3B1359DA5DDB` | decompiled RimWorld/Unity game code (third-party); regenerate with ilspycmd 9.1.0.7988 |
| `Graphify/native/source/Verse.Projectile.cs` | 16008 | `C989FB871691AC1A8FF1F9B3B8CDE2B6070D64D748DF0419C0DE2F6B8EF13FB4` | decompiled RimWorld/Unity game code (third-party); regenerate with ilspycmd 9.1.0.7988 |
| `Graphify/native/source/Verse.ProjectileHitFlags.cs` | 685 | `AF3FF7FFF732284EA391521589F734909BDAB3358C6DAEBE3C21117CCE719EAF` | decompiled RimWorld/Unity game code (third-party); regenerate with ilspycmd 9.1.0.7988 |
| `Graphify/native/source/Verse.Thing.cs` | 58085 | `9D21FC4188D16A1CF772E41CE91FB48D1F48C2184C27D134C70878F02762457F` | decompiled RimWorld/Unity game code (third-party); regenerate with ilspycmd 9.1.0.7988 |
| `Graphify/native/source/Verse.ThingWithComps.cs` | 21917 | `F8109FE2E767D3A1302ABA0A1687A36AE087741072713488DF1DC312A83A5AD9` | decompiled RimWorld/Unity game code (third-party); regenerate with ilspycmd 9.1.0.7988 |
| `Graphify/graphify-out/graph.json` | 1244435 | `3EA26FD826130CAAC62D76E0961163B8ADDCA53B6626697C801599CF5A269E3F` | generated graph cache; regenerate with refresh-graph.ps1 |
| `Graphify/graphify-out/metadata.json` | 16614 | `EFA2F0271C296182788FCC663952A9474E3B715957271304C3FE9908CD593479` | generated graph cache; regenerate with refresh-graph.ps1 |
| `Graphify\tools\decompiler\ilspycmd.9.1.0.7988.nupkg` |  |  | third-party decompiler tool (missing) |
| `tools/dotnet-sdk-10.0.402-win-x64.zip` | 300755826 | `24D057AB968986A9D7C7A01606B2A0192CBE6485221BA38E9C685522E93D8798` | 300MB SDK archive |
| `tools/check-missiles/out-a20/WhisperOfTheForest.dll` | 122880 | `C08464FDFB2A51BF77951623D0A7C35B33FF91D18B2BC9DF86C45876F51EB0C1` | build output; the shipped mod DLL lives at 1.7/Assemblies |
| `tools/check-lab/out/WhisperOfTheForest.dll` | 121344 | `CE991A866E9160F69F0CD7D72C522CEB9D4E01B55C75A60D372ACC017CCE3074` | build output |
| `tools/evidence/driftcheck/out/WhisperOfTheForest.dll` | 113664 | `57C0DFA4084215C85DD1023688261E8C0567039ECD94F9A6C8177C52B27DBBF6` | build output |
| `tools/check-missiles/ru-all.txt` | 60391 | `69EC84C7C9298F80A230E62599ABED3974904CC722FF32479B3091C8FE274FAA` | decompiled game API dump (60KB) used as checker input; regenerate with dump.ps1/il.ps1 |
| `tools/check-missiles/abilitydef.txt` | 5160 | `5050EA0F9515F68BCC547404ACC19DA041DA16D53B2714BB8917C6B8D25E7097` | decompiled game API field dump used by check_defs.py |
| `tools/check-missiles/myfields.txt` | 1717 | `9692C717DF05A458CCC7A7E4621119CDE2511D3B2D268935D5EDC6FA60D514BB` | decompiled mod field dump used by check_defs.py |
| `tools/check-ui/api1.txt` | 25069 | `AB3241EE622ADAE8BF416E3458368BC99B5D2376259FF39BDFD5072B9166A580` | decompiled game API text dump |
| `tools/check-ui/window_il.txt` | 27423 | `D9326282A415017F8325304C46A3B58F2CD876FA95C4EE7BFBF3CC3DAEDB2E49` | decompiled IL dump |
| `tools/check-movement/il/pather.il` | 119181 | `658769E30F4FD5CF43B4D3642EF7BF992DB50D5D2469DF33B40B362597C89CBD` | decompiled IL dump |
| `tools/check-movement/il/thingdef-configerrors.il` | 89083 | `AB794BABAAF6213A3598CFD746222E5D57DE2C9E575DA9AFA6E4E7EDFB02F599` | decompiled IL dump |
| `tools/check-elf/definject-setpath.txt` | 53616 | `4F8CE001D027123B561A75D6D6067B4BADF43DBAC0DAFF4D552C0266521F7578` | decompiled game API text dump |
| `lab-profile/TextureCache/v47/bc7fbfgnobc1k/pages.755665c23223ad99.zip` | 52084293 | （文件被占用，未计算） | 52MB lab texture cache |

另有整目录未上传（无单文件哈希）：

- `Moddev/lab-profile/`：实验用 RimWorld 配置目录（`Config/`、`Saves/`、`TextureCache/` 52 MB 缓存等），属机器本地状态。
- `Moddev/tools/dotnet/`、`Moddev/tools/dotnet-home/`、`Moddev/tools/dotnet-sdk-10.0.402-win-x64.zip`（300 MB）：便携 .NET SDK，新机器自装 10.0.x 即可。
- `Moddev/Graphify/graphify-out/`：graph.json、AST cache、`runs/`、venv 等生成物，见 `Graphify/NOTES.md` 重建。
- `Moddev/Graphify/tools/decompiler/`：ilspycmd 9.1.0.7988 工具安装（第三方）。
- `Moddev/tools/*/bin`、`obj`、`out*`：构建中间产物。

## 四、取舍理由摘要

- **结果 JSONL 全量带上**（约 5.4 MB）：纯文本、可复核、带命令 ID，是实验结论的直接证据；单个文件都很小，已复查不含用户目录路径（只有本机实验目录 E:/…/Moddev/…）。
- **重复文件去重**：`evidence/a20-prelaunch/results.jsonl.a19` 与 `a19-soundfix/results.jsonl` 哈希完全相同，只留一份。
- **日志只留摘录**：完整 Player.log 含本机路径与整份模组列表且体积大；摘录保留与本模组相关的行与错误统计，文件头写明源文件哈希。
- **存档不上传**：单个 6–22 MB，含实验现场与本机配置；文件名、大小、哈希全部记录在上表，重放路径见 HANDOFF.md 第 8 节。
- **反编译产物不上传**：`Graphify/native/source/*.cs`、`*.il`、`*.txt` 字段/IL dump 都是第三方游戏代码输出，可按 Tools/README.md 与 Graphify/NOTES.md 的方法在本地重建。
