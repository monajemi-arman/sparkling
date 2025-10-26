# Help & Documentation
## Jupyter Spark
* Use the following code in the embedded jupyter notebook to use Spark with GPU:
```python
from pyspark.sql import SparkSession

spark = SparkSession.builder \
    .appName("MySparkApp") \
    .master("spark://spark-master:7077") \
    .config("spark.task.resource.gpu.amount", "1") \
    .config("spark.executor.resource.gpu.amount", "1") \
    .getOrCreate()
```