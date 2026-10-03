<?php
class T {
    private $name;
    public function coalesce($input) { $this->name = $this->name ?? $input; return $this->name ?? ""; }
    public function coalesceAssign($input) { $this->name ??= $input; return $this->name ?? ""; }
    public function plain($input) { return $input; }
    public function andOp($a, $b) { return $a && $b; }
}
