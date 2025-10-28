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

* After creating the spark session as above, you may write your training code. Here is an example that trains a dummy CNN on dummy datset:
```python
from pyspark.ml.torch.distributor import TorchDistributor
import os
import torch
import torch.nn as nn

# Dummy createModel function
def createModel():
    """Create a simple dummy model if the original is not available"""
    return nn.Sequential(
        nn.Linear(10, 50),
        nn.ReLU(),
        nn.Linear(50, 2)
    )

def train(learning_rate, use_gpu):
    import torch
    import torch.distributed as dist
    from torch import nn
    from torch.utils.data import DistributedSampler, DataLoader
    
    # Dummy dataset - replace with your actual dataset
    class DummyDataset(torch.utils.data.Dataset):
        def __len__(self):
            return 1000
        
        def __getitem__(self, idx):
            return torch.randn(10), torch.randint(0, 2, (1,))
    
    # Initialize process group
    DDP = nn.parallel.DistributedDataParallel
    backend = "nccl" if use_gpu else "gloo"
    dist.init_process_group(backend)
    
    # Set device
    device = torch.device(f"cuda:{os.environ['LOCAL_RANK']}" if use_gpu else "cpu")
    
    # Create model with DDP
    model = createModel().to(device)
    if use_gpu:
        model = DDP(model, device_ids=[int(os.environ['LOCAL_RANK'])])
    
    # Create dummy dataset and dataloader
    dataset = DummyDataset()
    sampler = DistributedSampler(dataset)
    loader = DataLoader(dataset, batch_size=32, sampler=sampler)
    
    # Simple training loop
    criterion = nn.CrossEntropyLoss()
    optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
    
    model.train()
    for epoch in range(10):  # Short training for demo
        sampler.set_epoch(epoch)
        for batch_idx, (data, target) in enumerate(loader):
            data, target = data.to(device), target.to(device)
            optimizer.zero_grad()
            output = model(data)
            loss = criterion(output, target.squeeze())
            loss.backward()
            optimizer.step()
            
            if batch_idx % 50 == 0:
                print(f"Epoch: {epoch}, Batch: {batch_idx}, Loss: {loss.item()}")
    
    output = f"Training completed with final loss: {loss.item()}"
    
    # FIX: Use destroy_process_group instead of cleanup
    dist.destroy_process_group()
    
    return output

# Run the distributor
distributor = TorchDistributor(num_processes=1, local_mode=False, use_gpu=True)
result = distributor.run(train, 1e-3, True)
print(result)
```