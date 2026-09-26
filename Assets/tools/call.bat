@echo off

python .\generate_training_responses.py ^
  --input "C:\Users\mshan\AppData\LocalLow\DefaultCompany\CodysseyFinalProject\CodysseyTraining\codyssey_training_bundles.txt" ^
  --output ".\training_responses.json" ^
  --model "gpt-6-luna" ^
  --count 10

pause
