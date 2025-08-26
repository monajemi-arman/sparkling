# Help & Documentation
## Jupyter Spark
* Use the following code in the embedded jupyter notebook to use Spark:
```python
from pyspark.sql import SparkSession
spark = SparkSession.builder \
    .appName("MySparkApp") \
    .getOrCreate()
```