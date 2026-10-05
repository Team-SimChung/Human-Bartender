# CSV 전환 검증

- `CsvContent.EditMode.xml`: Unity 6000.3.14f1, 별도 프로젝트 복사본에서 CsvContentTests 7개 통과.
- `CsvContent.Parity.txt`: 기준 데이터 37개와 변환값 일치, 로더 대상 C# 타입 30개 결과 일치, CSV 구문·연결 오류 거부 결과.
- 두 최종 XLSX를 다시 읽어 기존 값 보존, 대사 편집, 새 행 추가, CSV 재읽기와 잘못된 입력 거부를 확인했습니다.
- 두 엑셀의 51개 시트를 렌더링해 54개 미리보기를 확인했습니다. 수식 오류 검색 결과는 0개입니다.

프로젝트에서 다시 실행할 수 있는 검사는 Unity의 **Tools → Data → Validate CSV Content**와 Test Runner의 **CsvContentTests**입니다.
CSV만으로 구조를 검사하려면 프로젝트 루트에서 아래 명령을 실행합니다.

```sh
python3 outputs/20260915-content-editing/export_content.py --source csv --input Assets/StreamingAssets/csv --validate-only
```

조건식의 실제 실행·씬 도달·리소스 재생과 모바일 빌드는 이 데이터 검증 범위에 포함하지 않습니다.
